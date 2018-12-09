﻿using Prism.Mvvm;
using Prism.Regions;

namespace CapFrameX.ViewModel
{
	public class ComparisonDataViewModel : BindableBase, INavigationAware
	{
		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			
		}
	}
}