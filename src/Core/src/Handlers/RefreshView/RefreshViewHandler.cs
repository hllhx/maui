﻿#if __IOS__ || MACCATALYST
using PlatformView = Microsoft.Maui.Platform.MauiRefreshView;
#elif MONOANDROID
using PlatformView = Microsoft.Maui.Platform.MauiSwipeRefreshLayout;
#elif WINDOWS
using PlatformView = Microsoft.UI.Xaml.Controls.RefreshContainer;
#elif NETSTANDARD || (NET6_0 && !IOS && !ANDROID)
using PlatformView = System.Object;
#endif

namespace Microsoft.Maui.Handlers
{
	public partial class RefreshViewHandler : IRefreshViewHandler
	{
		public static IPropertyMapper<IRefreshView, IRefreshViewHandler> Mapper = new PropertyMapper<IRefreshView, IRefreshViewHandler>(ViewHandler.ViewMapper)
		{
			[nameof(IRefreshView.IsRefreshing)] = MapIsRefreshing,
			[nameof(IRefreshView.Content)] = MapContent,
			[nameof(IRefreshView.RefreshColor)] = MapRefreshColor,
			[nameof(IView.Background)] = MapBackground,
			[nameof(IView.IsEnabled)] = MapIsEnabled,
		};

		public static CommandMapper<IRefreshView, IRefreshViewHandler> CommandMapper = new(ViewCommandMapper)
		{
		};

		public RefreshViewHandler() : base(Mapper, CommandMapper)
		{
		}

		public RefreshViewHandler(IPropertyMapper? mapper = null) : base(mapper ?? Mapper)
		{
		}

		IRefreshView IRefreshViewHandler.VirtualView => VirtualView;

		PlatformView IRefreshViewHandler.PlatformView => PlatformView;
	}
}
