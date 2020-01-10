﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		private readonly IStatisticProvider _statisticProvider;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IOverlayEntryProvider _overlayEntryProvider;
		private readonly IAppConfiguration _appConfiguration;

		private IDisposable _disposableHeartBeat;
		private IDisposable _disposableCaptureTimer;
		private IDisposable _disposableCountdown;

		private IList<string> _runHistory = new List<string>();
		private IList<IList<string>> _captureDataHistory = new List<IList<string>>();
		private IList<IList<double>> _frametimeHistory = new List<IList<double>>();
		private bool[] _runHistoryOutlierFlags;
		private int _refreshPeriod;
		private int _numberOfRuns;
		private IList<MetricAnalysis> _metricAnalysis = new List<MetricAnalysis>();

		public Subject<bool> IsOverlayActiveStream { get; }

		public string SecondMetric { get; set; }

		public string ThirdMetric { get; set; }

		public int RunHistoryCount => _runHistory.Count(run => run != "N/A");

		public OverlayService(IStatisticProvider statisticProvider, IRecordDataProvider recordDataProvider,
			IOverlayEntryProvider overlayEntryProvider, IAppConfiguration appConfiguration)
			: base()
		{
			_statisticProvider = statisticProvider;
			_recordDataProvider = recordDataProvider;
			_overlayEntryProvider = overlayEntryProvider;
			_appConfiguration = appConfiguration;

			_refreshPeriod = _appConfiguration.OSDRefreshPeriod;
			_numberOfRuns = _appConfiguration.SelectedHistoryRuns;
			SecondMetric = _appConfiguration.SecondMetricOverlay;
			ThirdMetric = _appConfiguration.ThirdMetricOverlay;
			IsOverlayActiveStream = new Subject<bool>();
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();

			SetOverlayEntries(overlayEntryProvider?.GetOverlayEntries());
			overlayEntryProvider.EntryUpdateStream.Subscribe(x =>
				SetOverlayEntries(overlayEntryProvider?.GetOverlayEntries()));

			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
		}

		public void ShowOverlay()
		{
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void HideOverlay()
		{
			_disposableHeartBeat?.Dispose();
			ReleaseOSD();
		}

		public void UpdateRefreshRate(int milliSeconds)
		{
			_refreshPeriod = milliSeconds;
			_disposableHeartBeat?.Dispose();
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void StartCountdown(int seconds)
		{
			IObservable<long> obs = Extensions.ObservableExtensions.CountDown(seconds);
			SetShowCaptureTimer(true);

			SetCaptureTimerValue(0);
			_disposableCountdown?.Dispose();
			_disposableCountdown = obs.Subscribe(t =>
			{
				SetCaptureTimerValue((int)t);

				if (t == 0)
					SetShowCaptureTimer(false);
			});
		}

		public void StartCaptureTimer()
		{
			SetShowCaptureTimer(true);
			_disposableCaptureTimer = GetCaptureTimer();
		}

		public void StopCaptureTimer()
		{
			SetShowCaptureTimer(false);
			SetCaptureTimerValue(0);
			_disposableCaptureTimer?.Dispose();
		}

		public void SetCaptureTimerValue(int t)
		{
			var captureTimer = _overlayEntryProvider.GetOverlayEntry("CaptureTimer");
			captureTimer.Value = $"{t.ToString()} s";
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
			if (_appConfiguration.IsOverlayActive)
			{
				CheckRTSSRunningAndRefresh();
			};
		}

		public void SetCaptureServiceStatus(string status)
		{
			var captureStatus = _overlayEntryProvider.GetOverlayEntry("CaptureServiceStatus");
			captureStatus.Value = status;
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
		}

		public void SetShowRunHistory(bool showHistory)
		{
			var history = _overlayEntryProvider.GetOverlayEntry("RunHistory");
			history.ShowOnOverlay = showHistory;
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
		}

		public void ResetHistory()
		{
			_runHistory = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
			_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();
			_captureDataHistory.Clear();
			_frametimeHistory.Clear();
			_metricAnalysis.Clear();
			SetRunHistory(_runHistory.ToArray());
			SetRunHistoryAggregation(string.Empty);
			SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
		}

		public void AddRunToHistory(List<string> captureData)
		{
			var frametimes = captureData.Select(line =>
				RecordDataProvider.GetFrameTimeFromDataLine(line)).ToList();

			if (RunHistoryCount == _numberOfRuns)
			{
				if (!_runHistoryOutlierFlags.All(x => x == false)
					&& _appConfiguration.OutlierHandling == EOutlierHandling.Replace.ConvertToString())
				{
					var historyDefault = Enumerable.Repeat("N/A", _numberOfRuns).ToList();
					var validRuns = _runHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false).ToList();

					for (int i = 0; i < validRuns.Count; i++)
					{
						historyDefault[i] = validRuns[i];
					}

					var validCaptureData = _captureDataHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false);
					var validFrametimes = _frametimeHistory.Where((run, i) => _runHistoryOutlierFlags[i] == false);
					var validMetricAnalysis = _metricAnalysis.Where((run, i) => _runHistoryOutlierFlags[i] == false);					

					_runHistory = historyDefault.ToList();
					_captureDataHistory = validCaptureData.ToList();
					_frametimeHistory = validFrametimes.ToList();
					_metricAnalysis = validMetricAnalysis.ToList();

					// local reset
					_runHistoryOutlierFlags = Enumerable.Repeat(false, _numberOfRuns).ToArray();

					SetRunHistory(_runHistory.ToArray());
					SetRunHistoryAggregation(string.Empty);
					SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);
				}
				else
				{
					ResetHistory();
				}
			}

			if (RunHistoryCount < _numberOfRuns)
			{
				// metric history
				var currentAnalysis = GetMetrics(frametimes);
				_metricAnalysis.Add(currentAnalysis);
				_runHistory[RunHistoryCount] = currentAnalysis.ResultString;
				SetRunHistory(_runHistory.ToArray());

				// capture data history
				_captureDataHistory.Add(captureData);

				// frametime history
				_frametimeHistory.Add(frametimes);

				if (_appConfiguration.UseAggregation
					&& RunHistoryCount == _numberOfRuns)
				{
					CalculateOutlierAnalysis();
					SetRunHistoryOutlierFlags(_runHistoryOutlierFlags);

					if ((_runHistoryOutlierFlags.All(x => x == false)))
					{
						SetRunHistoryAggregation(GetAggregation());

						// write aggregated file
						Task.Run(async () =>
						{
							await SetTaskDelayOffset().ContinueWith(_ =>
							{
								_recordDataProvider
								.SaveAggregatedPresentData(_captureDataHistory);
							}, CancellationToken.None, TaskContinuationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
						});
					}
				}
			}
		}

		public void UpdateOverlayEntries()
		{
			SetOverlayEntries(_overlayEntryProvider?.GetOverlayEntries());
		}

		public void UpdateNumberOfRuns(int numberOfRuns)
		{
			_numberOfRuns = numberOfRuns;
			ResetHistory();
		}

		private void CalculateOutlierAnalysis()
		{
			var averageValues = _metricAnalysis.Select(analysis => analysis.Average).ToList();
			var secondMetricValues = _metricAnalysis.Select(analysis => analysis.Second).ToList();
			var thirdMetricValues = _metricAnalysis.Select(analysis => analysis.Third).ToList();

			if (_appConfiguration.RelatedMetric == "Average")
			{
				SetOutlierFlags(averageValues);
			}
			else if (_appConfiguration.RelatedMetric == "Second")
			{
				SetOutlierFlags(secondMetricValues);
			}
			else if (_appConfiguration.RelatedMetric == "Third")
			{
				SetOutlierFlags(thirdMetricValues);
			}
		}

		private void SetOutlierFlags(List<double> values)
		{
			var median = _statisticProvider.GetPQuantileSequence(values, 0.5);

			for (int i = 0; i < values.Count; i++)
			{
				if ((Math.Abs(values[i] - median) / median) * 100 > (double)_appConfiguration.OutlierPercentage)
				{
					_runHistoryOutlierFlags[i] = true;
				}
			}
		}

		private async Task SetTaskDelayOffset()
		{
			await Task.Delay(TimeSpan.FromMilliseconds(1000));
		}

		private void SetShowCaptureTimer(bool show)
		{
			var captureTimer = _overlayEntryProvider.GetOverlayEntry("CaptureTimer");
			captureTimer.ShowOnOverlay = show;
		}

		private void CheckRTSSRunningAndRefresh()
		{
			var processes = Process.GetProcessesByName("RTSS");

			if (!processes.Any())
			{
				try
				{
					Process proc = new Process();
					proc.StartInfo.FileName = Path.Combine(RTSSUtils.GetRTSSFullPath());
					proc.StartInfo.UseShellExecute = false;
					proc.StartInfo.Verb = "runas";
					proc.Start();
				}
				catch { }
			}

			Refresh();
		}

		private IDisposable GetOverlayRefreshHeartBeat()
		{
			CheckRTSSRunningAndRefresh();

			return Observable
				.Timer(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(_refreshPeriod))
				.Subscribe(x => CheckRTSSRunningAndRefresh());
		}

		private IDisposable GetCaptureTimer()
		{
			return Observable
				.Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
				.Subscribe(x => SetCaptureTimerValue((int)x));
		}

		private string GetAggregation()
		{
			var concatedFrametimes = new List<double>(_frametimeHistory.Sum(set => set.Count));

			foreach (var frametimeSet in _frametimeHistory)
			{
				concatedFrametimes.AddRange(frametimeSet);
			}

			return GetMetrics(concatedFrametimes).ResultString;
		}

		private MetricAnalysis GetMetrics(List<double> frametimes)
		{
			var average = _statisticProvider.GetFpsMetricValue(frametimes, EMetric.Average);
			var secondMetricValue = _statisticProvider.GetFpsMetricValue(frametimes, SecondMetric.ConvertToEnum<EMetric>());
			var thrirdMetricValue = _statisticProvider.GetFpsMetricValue(frametimes, ThirdMetric.ConvertToEnum<EMetric>());
			string numberFormat = string.Format("F{0}", _appConfiguration.FpsValuesRoundingDigits);
			var cultureInfo = CultureInfo.InvariantCulture;

			string secondMetricString =
				SecondMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{SecondMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{secondMetricValue.ToString(numberFormat, cultureInfo)} " +
				$"FPS | " : string.Empty;

			string thirdMetricString =
				ThirdMetric.ConvertToEnum<EMetric>() != EMetric.None ?
				$"{ThirdMetric.ConvertToEnum<EMetric>().GetShortDescription()}=" +
				$"{thrirdMetricValue.ToString(numberFormat, cultureInfo)} " +
				$"FPS" : string.Empty;

			return new MetricAnalysis()
			{
				ResultString = $"Avg={average.ToString(numberFormat, cultureInfo)} " +
				$"FPS | " + secondMetricString + thirdMetricString,
				Average = average,
				Second = secondMetricValue,
				Third = thrirdMetricValue
			};
		}

		private class MetricAnalysis
		{
			public string ResultString { get; set; }
			public double Average { get; set; }
			public double Second { get; set; }
			public double Third { get; set; }
		}
	}
}
