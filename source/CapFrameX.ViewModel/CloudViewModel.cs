﻿using CapFrameX.Contracts.Cloud;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using GongSolutions.Wpf.DragDrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
	public class CloudViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private readonly IStatisticProvider _statisticProvider;
		private readonly IRecordDataProvider _recordDataProvider;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool _useUpdateSession;
		private int _selectedCloudEntryIndex = -1;
		private bool _showHelpText = true;
		private List<IFileRecordInfo> _fileRecordInfoList = new List<IFileRecordInfo>();

		public int SelectedCloudEntryIndex
		{
			get
			{return _selectedCloudEntryIndex;}
			set
			{
				_selectedCloudEntryIndex = value;
				RaisePropertyChanged();
			}
		}

		public bool ShowHelpText
		{
			get
			{return _showHelpText;}
			set
			{
				_showHelpText = value;
				RaisePropertyChanged();
			}
		}


		public ObservableCollection<ICloudEntry> CloudEntries { get; private set; }
			= new ObservableCollection<ICloudEntry>();

		public CloudViewModel(IStatisticProvider statisticProvider, IRecordDataProvider recordDataProvider,
			IEventAggregator eventAggregator, IAppConfiguration appConfiguration)
		{
			_statisticProvider = statisticProvider;
			_recordDataProvider = recordDataProvider;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;


			SubscribeToUpdateSession();

			CloudEntries.CollectionChanged += new NotifyCollectionChangedEventHandler
				((sender, eventArg) => OnCloudEntriesChanged());
		}

		public void RemoveCloudEntry(ICloudEntry entry)
		{
			_fileRecordInfoList.Remove(entry.FileRecordInfo);
			CloudEntries.Remove(entry);
		}

		private void OnCloudEntriesChanged()
		{
			ShowHelpText = !CloudEntries.Any();

		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
							.Subscribe(msg =>
							{
								if (_useUpdateSession)
								{
									AddCloudEntry(msg.RecordInfo, msg.CurrentSession);
								}
							});
		}

		private void AddCloudEntry(IFileRecordInfo recordInfo, Session session)
		{
			if (recordInfo != null)
			{
				_fileRecordInfoList.Add(recordInfo);
			}
			else
				return;


			List<double> frametimes = session?.FrameTimes;

			if (session == null)
			{
				var localSession = RecordManager.LoadData(recordInfo.FullPath);
				frametimes = localSession?.FrameTimes;
			}

			CloudEntries.Add(new CloudEntry()
			{
				GameName = recordInfo.GameName,
				CreationDate = recordInfo.CreationDate,
				CreationTime = recordInfo.CreationTime,
				Comment = recordInfo.Comment,
				FileRecordInfo = recordInfo
			});
		}

		private void OnClearTable()
		{
			CloudEntries.Clear();
			_fileRecordInfoList.Clear();
		}


		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useUpdateSession = true;
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			_useUpdateSession = false;
		}

		void IDropTarget.Drop(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
				{
					if (frameworkElement.Name == "CloudItemDataGrid"
						|| frameworkElement.Name == "DragAndDropInfoTextTextBlock")
					{
						if (dropInfo.Data is IFileRecordInfo recordInfo)
						{
							AddCloudEntry(recordInfo, null);
						}
					}
				}
			}
		}

		void IDropTarget.DragOver(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
				dropInfo.Effects = DragDropEffects.Move;
			}
		}
	}
}