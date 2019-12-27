﻿using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		Subject<bool> IsOverlayActiveStream { get; }

		string SecondMetric { get; set; }

		string ThirdMetric { get; set; }

		void ShowOverlay();

		void HideOverlay();

		void UpdateRefreshRate(int milliSeconds);

		void UpdateNumberOfRuns(int numberOfRuns);

		void SetCaptureTimerValue(int t);

		void StartCountdown(int seconds);

		void StartCaptureTimer();

		void StopCaptureTimer();

		void SetCaptureServiceStatus(string status);

		void SetShowRunHistory(bool showHistory);

		void ResetHistory();

		void AddRunToHistory(List<string> captureData);
	}
}
