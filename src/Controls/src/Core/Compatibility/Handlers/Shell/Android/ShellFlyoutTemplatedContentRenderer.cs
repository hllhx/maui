using System;
using System.ComponentModel;
using Android.Content;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.CoordinatorLayout.Widget;
using AndroidX.DrawerLayout.Widget;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.AppBar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Controls.Platform.Compatibility;
using AView = Android.Views.View;
using LP = Android.Views.ViewGroup.LayoutParams;

namespace Microsoft.Maui.Controls.Platform.Compatibility
{
	public class ShellFlyoutTemplatedContentRenderer : Java.Lang.Object, IShellFlyoutContentRenderer
		, AppBarLayout.IOnOffsetChangedListener
	{
		#region IShellFlyoutContentView

		public AView AndroidView => _rootView;

		#endregion IShellFlyoutContentView

		IShellContext _shellContext;
		bool _disposed;
		HeaderContainer _headerView;
		ViewGroup _rootView;
		Drawable _defaultBackgroundColor;
		ImageView _bgImage;
		AppBarLayout _appBar;
		AView _flyoutContentView;
		ShellViewRenderer _contentView;
		View _flyoutHeader;
		ShellViewRenderer _footerView;
		int _actionBarHeight;
		int _flyoutHeight;
		int _flyoutWidth;
		protected IMauiContext MauiContext => _shellContext.Shell.Handler.MauiContext;
		bool _initialLayoutChangeFired;
		IFlyoutView FlyoutView => _shellContext?.Shell;
		protected IShellContext ShellContext => _shellContext;
		protected AView FooterView => _footerView?.PlatformView;
		protected AView View => _rootView;


		public ShellFlyoutTemplatedContentRenderer(IShellContext shellContext)
		{
			_shellContext = shellContext;
			_shellContext.CurrentDrawerLayout.DrawerStateChanged += OnFlyoutStateChanging;
			LoadView(shellContext);
		}

		void OnFlyoutStateChanging(object sender, AndroidX.DrawerLayout.Widget.DrawerLayout.DrawerStateChangedEventArgs e)
		{
			if (e.NewState != DrawerLayout.StateIdle)
			{
				if (_flyoutContentView == null)
					UpdateFlyoutContent();

				_shellContext.CurrentDrawerLayout.DrawerStateChanged -= OnFlyoutStateChanging;
			}
		}

		protected virtual void LoadView(IShellContext shellContext)
		{
			var context = shellContext.AndroidContext;
			var layoutInflator = shellContext.Shell.FindMauiContext().GetLayoutInflater();
			var coordinator = (ViewGroup)layoutInflator.Inflate(Controls.Resource.Layout.flyoutcontent, null);

			_appBar = coordinator.FindViewById<AppBarLayout>(Controls.Resource.Id.flyoutcontent_appbar);

			_rootView = coordinator as ViewGroup;

			_appBar.AddOnOffsetChangedListener(this);

			_actionBarHeight = context.GetActionBarHeight();
			UpdateFlyoutHeader();

			var metrics = context.Resources.DisplayMetrics;
			var width = Math.Min(metrics.WidthPixels, metrics.HeightPixels);

			width -= _actionBarHeight;

			coordinator.LayoutParameters = new LP(width, LP.MatchParent);

			_bgImage = new ImageView(context)
			{
				LayoutParameters = new LP(coordinator.LayoutParameters)
			};

			UpdateFlyoutHeaderBehavior();
			_shellContext.Shell.PropertyChanged += OnShellPropertyChanged;

			UpdateFlyoutBackground();

			UpdateVerticalScrollMode();

			UpdateFlyoutFooter();

			if (FlyoutView.FlyoutBehavior == FlyoutBehavior.Locked)
				OnFlyoutViewLayoutChanged();
			else if (View is ShellFlyoutLayout sfl)
				sfl.LayoutChanging += OnFlyoutViewLayoutChanged;
		}

		void OnFlyoutHeaderMeasureInvalidated(object sender, EventArgs e)
		{
			if (_headerView != null)
				UpdateFlyoutHeaderBehavior();
		}

		protected void OnElementSelected(Element element)
		{
			((IShellController)_shellContext.Shell).OnFlyoutItemSelected(element);
		}

		protected virtual void OnShellPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Shell.FlyoutHeaderBehaviorProperty.PropertyName)
				UpdateFlyoutHeaderBehavior();
			else if (e.IsOneOf(
				Shell.FlyoutBackgroundColorProperty,
				Shell.FlyoutBackgroundProperty,
				Shell.FlyoutBackgroundImageProperty,
				Shell.FlyoutBackgroundImageAspectProperty))
				UpdateFlyoutBackground();
			else if (e.Is(Shell.FlyoutVerticalScrollModeProperty))
				UpdateVerticalScrollMode();
			else if (e.IsOneOf(
				Shell.FlyoutHeaderProperty,
				Shell.FlyoutHeaderTemplateProperty))
				UpdateFlyoutHeader();
			else if (e.IsOneOf(
				Shell.FlyoutFooterProperty,
				Shell.FlyoutFooterTemplateProperty))
				UpdateFlyoutFooter();
			else if (e.IsOneOf(
				Shell.FlyoutContentProperty,
				Shell.FlyoutContentTemplateProperty))
				UpdateFlyoutContent();
		}

		protected virtual void UpdateFlyoutContent()
		{
			if (!_rootView.IsAlive())
				return;

			var index = 0;
			if (_flyoutContentView != null)
			{
				index = _rootView.IndexOfChild(_flyoutContentView);
				_rootView.RemoveView(_flyoutContentView);
			}

			_flyoutContentView = CreateFlyoutContent(_rootView);
			if (_flyoutContentView == null)
				return;

			_rootView.AddView(_flyoutContentView, index);
			UpdateContentLayout();
		}

		AView CreateFlyoutContent(ViewGroup rootView)
		{
			_rootView = rootView;
			if (_contentView != null)
			{
				var oldContentView = _contentView;
				_contentView = null;
				oldContentView.TearDown();
			}

			var content = ((IShellController)ShellContext.Shell).FlyoutContent;
			if (content == null)
			{
				var lp = new CoordinatorLayout.LayoutParams(CoordinatorLayout.LayoutParams.MatchParent, CoordinatorLayout.LayoutParams.MatchParent);
				lp.Behavior = new AppBarLayout.ScrollingViewBehavior();
				var context = ShellContext.AndroidContext;
				var recyclerView = new RecyclerViewContainer(context)
				{
					LayoutParameters = lp
				};

				recyclerView.SetAdapter(new ShellFlyoutRecyclerAdapter(ShellContext, OnElementSelected));

				return recyclerView;
			}

			_contentView = new ShellViewRenderer(ShellContext.AndroidContext, content, MauiContext);

			_contentView.PlatformView.LayoutParameters = new CoordinatorLayout.LayoutParams(LP.MatchParent, LP.MatchParent)
			{
				Behavior = new AppBarLayout.ScrollingViewBehavior()
			};

			return _contentView.PlatformView;
		}

		protected virtual void UpdateFlyoutHeader()
		{
			if (_headerView != null)
			{
				var oldHeaderView = _headerView;
				_headerView = null;
				_appBar.RemoveView(oldHeaderView);
				oldHeaderView.Dispose();
			}

			if (_flyoutHeader != null)
			{
				_flyoutHeader.MeasureInvalidated -= OnFlyoutHeaderMeasureInvalidated;
			}

			_flyoutHeader = ((IShellController)_shellContext.Shell).FlyoutHeader;
			if (_flyoutHeader != null)
				_flyoutHeader.MeasureInvalidated += OnFlyoutHeaderMeasureInvalidated;

			_headerView = new HeaderContainer(_shellContext.AndroidContext, _flyoutHeader, MauiContext)
			{
				MatchWidth = true
			};

			_headerView.SetMinimumHeight(_actionBarHeight);
			_headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
			{
				ScrollFlags = AppBarLayout.LayoutParams.ScrollFlagScroll
			};
			_appBar.AddView(_headerView);
			UpdateFlyoutHeaderBehavior();

			UpdateContentLayout();
		}

		protected virtual void UpdateFlyoutFooter()
		{
			if (_footerView != null)
			{
				var oldFooterView = _footerView;
				_rootView.RemoveView(_footerView.PlatformView);
				_footerView = null;
				oldFooterView.TearDown();
			}

			var footer = ((IShellController)_shellContext.Shell).FlyoutFooter;

			if (footer == null)
				return;

			_footerView = new ShellViewRenderer(_shellContext.AndroidContext, footer, MauiContext);

			_rootView.AddView(_footerView.PlatformView);

			if (_footerView.PlatformView.LayoutParameters is CoordinatorLayout.LayoutParams cl)
				cl.Gravity = (int)(GravityFlags.Bottom | GravityFlags.End);

			UpdateFooterLayout();
			UpdateContentLayout();
		}

		void UpdateFooterLayout()
		{
			_ = _footerView?
					.Measure(MeasureSpecMode.Exactly
					.MakeMeasureSpec(_rootView.LayoutParameters.Width), MeasureSpecMode.Unspecified.MakeMeasureSpec(0), null, null);

		}

		void UpdateContentLayout()
		{
			if (_contentView != null)
			{
				if (_contentView == null)
					return;

				var height =
					(View.MeasuredHeight) -
					(FooterView?.MeasuredHeight ?? 0) -
					(_headerView?.MeasuredHeight ?? 0);

				var width = View.MeasuredWidth;

				if (_flyoutContentView?.LayoutParameters is ViewGroup.MarginLayoutParams cl)
					cl.BottomMargin = FooterView?.MeasuredHeight ?? 0;

				var frameSize = _contentView.Measure(
					MeasureSpecMode.Exactly.MakeMeasureSpec(width),
					MeasureSpecMode.Exactly.MakeMeasureSpec(height), null, null);

				_contentView.View.Frame = new Graphics.Rect(Graphics.Point.Zero, frameSize);
			}
		}

		void OnFlyoutViewLayoutChanged()
		{
			// The second time this fires the non flyout part of the view
			// is visible to the user. I haven't found a better
			// mechanism to wire into in order to detect this
			if ((_initialLayoutChangeFired || FlyoutView.FlyoutBehavior == FlyoutBehavior.Locked) &&
				_flyoutContentView == null)
			{
				UpdateFlyoutContent();
			}

			_initialLayoutChangeFired = true;

			if (View?.MeasuredHeight > 0 &&
				View?.MeasuredWidth > 0 &&
				(_flyoutHeight != View.MeasuredHeight ||
				_flyoutWidth != View.MeasuredWidth)
			)
			{
				_flyoutHeight = View.MeasuredHeight;
				_flyoutWidth = View.MeasuredWidth;

				UpdateFooterLayout();
				UpdateContentLayout();
			}
		}

		void UpdateVerticalScrollMode()
		{
			if (_flyoutContentView is RecyclerView rv && rv.GetLayoutManager() is ScrollLayoutManager lm)
			{
				lm.ScrollVertically = _shellContext.Shell.FlyoutVerticalScrollMode;
			}
		}

		protected virtual void UpdateFlyoutBackground()
		{
			var brush = _shellContext.Shell.FlyoutBackground;

			if (Brush.IsNullOrEmpty(brush))
			{
				var color = _shellContext.Shell.FlyoutBackgroundColor;
				if (_defaultBackgroundColor == null)
					_defaultBackgroundColor = _rootView.Background;

				_rootView.Background = color == null ? _defaultBackgroundColor : new ColorDrawable(color.ToPlatform());
			}
			else
				_rootView.UpdateBackground(brush);

			UpdateFlyoutBgImageAsync();
		}

		void UpdateFlyoutBgImageAsync()
		{
			var imageSource = _shellContext.Shell.FlyoutBackgroundImage;

			if (imageSource == null || !_shellContext.Shell.IsSet(Shell.FlyoutBackgroundImageProperty))
			{
				if (_rootView.IndexOfChild(_bgImage) != -1)
					_rootView.RemoveView(_bgImage);
				return;
			}

			var services = MauiContext.Services;
			var provider = services.GetRequiredService<IImageSourceServiceProvider>();

			_bgImage.Clear();
			imageSource.LoadImage(MauiContext, result =>
			{
				_bgImage.SetImageDrawable(result?.Value);

				if (!_rootView.IsAlive())
					return;

				if (result?.Value == null)
				{
					if (_rootView.IndexOfChild(_bgImage) != -1)
						_rootView.RemoveView(_bgImage);

					return;
				}

				switch (_shellContext.Shell.FlyoutBackgroundImageAspect)
				{
					default:
					case Aspect.AspectFit:
						_bgImage.SetScaleType(ImageView.ScaleType.FitCenter);
						break;
					case Aspect.AspectFill:
						_bgImage.SetScaleType(ImageView.ScaleType.CenterCrop);
						break;
					case Aspect.Fill:
						_bgImage.SetScaleType(ImageView.ScaleType.FitXy);
						break;
				}

				if (_rootView.IndexOfChild(_bgImage) == -1)
				{
					if (_bgImage.SetElevation(float.MinValue))
						_rootView.AddView(_bgImage);
					else
						_rootView.AddView(_bgImage, 0);
				}
			});
		}

		protected virtual void UpdateFlyoutHeaderBehavior()
		{
			var context = _shellContext.AndroidContext;

			var margin = _flyoutHeader?.Margin ?? default(Thickness);

			var minimumHeight = Convert.ToInt32(_actionBarHeight + context.ToPixels(margin.Top) - context.ToPixels(margin.Bottom));
			_headerView.SetMinimumHeight(minimumHeight);

			switch (_shellContext.Shell.FlyoutHeaderBehavior)
			{
				case FlyoutHeaderBehavior.Default:
				case FlyoutHeaderBehavior.Fixed:
					_headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
					{
						ScrollFlags = 0,
						LeftMargin = (int)context.ToPixels(margin.Left),
						TopMargin = (int)context.ToPixels(margin.Top),
						RightMargin = (int)context.ToPixels(margin.Right),
						BottomMargin = (int)context.ToPixels(margin.Bottom)
					};
					break;
				case FlyoutHeaderBehavior.Scroll:
					_headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
					{
						ScrollFlags = AppBarLayout.LayoutParams.ScrollFlagScroll,
						LeftMargin = (int)context.ToPixels(margin.Left),
						TopMargin = (int)context.ToPixels(margin.Top),
						RightMargin = (int)context.ToPixels(margin.Right),
						BottomMargin = (int)context.ToPixels(margin.Bottom)
					};
					break;
				case FlyoutHeaderBehavior.CollapseOnScroll:
					_headerView.LayoutParameters = new AppBarLayout.LayoutParams(LP.MatchParent, LP.WrapContent)
					{
						ScrollFlags = AppBarLayout.LayoutParams.ScrollFlagExitUntilCollapsed |
							AppBarLayout.LayoutParams.ScrollFlagScroll,
						LeftMargin = (int)context.ToPixels(margin.Left),
						TopMargin = (int)context.ToPixels(margin.Top),
						RightMargin = (int)context.ToPixels(margin.Right),
						BottomMargin = (int)context.ToPixels(margin.Bottom)
					};
					break;
			}
		}

		public void OnOffsetChanged(AppBarLayout appBarLayout, int verticalOffset)
		{
			var headerBehavior = _shellContext.Shell.FlyoutHeaderBehavior;
			if (headerBehavior != FlyoutHeaderBehavior.CollapseOnScroll)
				return;

			_headerView.SetPadding(0, -verticalOffset, 0, 0);
		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			_disposed = true;

			if (disposing)
			{
				_shellContext.Shell.PropertyChanged -= OnShellPropertyChanged;

				if (_flyoutHeader != null)
					_flyoutHeader.MeasureInvalidated -= OnFlyoutHeaderMeasureInvalidated;

				if (_appBar != null)
				{
					_appBar.RemoveOnOffsetChangedListener(this);
					_appBar.RemoveView(_headerView);
				}

				if (_rootView != null && _footerView?.PlatformView != null)
					_rootView.RemoveView(_footerView.PlatformView);

				if (View != null && View is ShellFlyoutLayout sfl)
					sfl.LayoutChanging -= OnFlyoutViewLayoutChanged;

				_contentView?.TearDown();
				_flyoutContentView?.Dispose();
				_headerView.Dispose();
				_footerView?.TearDown();
				_rootView.Dispose();
				_defaultBackgroundColor?.Dispose();
				_bgImage?.Dispose();

				_contentView = null;
				_flyoutHeader = null;
				_rootView = null;
				_headerView = null;
				_shellContext = null;
				_appBar = null;
				_flyoutContentView = null;
				_defaultBackgroundColor = null;
				_bgImage = null;
				_footerView = null;
			}

			base.Dispose(disposing);
		}

		// This view lets us use the top padding to "squish" the content down
		public class HeaderContainer : ContainerView
		{
			bool _isdisposed = false;
			public HeaderContainer(Context context, View view, IMauiContext mauiContext) : base(context, view, mauiContext)
			{
				Initialize(view);
			}

			void Initialize(View view)
			{
				if (view != null)
					view.PropertyChanged += OnViewPropertyChanged;
			}

			void OnViewPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				if (e.PropertyName == PlatformConfiguration.AndroidSpecific.VisualElement.ElevationProperty.PropertyName)
				{
					UpdateElevation();
				}
			}

			void UpdateElevation()
			{
				if (Parent is AView view)
					ElevationHelper.SetElevation(view, View);
			}

			protected override void OnLayout(bool changed, int l, int t, int r, int b)
			{
				l -= PaddingLeft + PaddingRight;
				t -= PaddingTop + PaddingBottom;

				UpdateElevation();
				base.OnLayout(changed, l, t, r, b);
			}

			protected override void Dispose(bool disposing)
			{
				if (_isdisposed)
					return;

				_isdisposed = true;
				if (disposing)
				{
					if (View != null)
						View.PropertyChanged -= OnViewPropertyChanged;
				}

				View = null;

				base.Dispose(disposing);
			}
		}
	}

	class RecyclerViewContainer : RecyclerView
	{
		bool _disposed;
		ScrollLayoutManager _layoutManager;

		public RecyclerViewContainer(Context context) : base(context)
		{
			SetClipToPadding(false);
			SetLayoutManager(_layoutManager = new ScrollLayoutManager(context, (int)Orientation.Vertical, false));
			SetLayoutManager(new LinearLayoutManager(context, (int)Orientation.Vertical, false));
		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			_disposed = true;
			if (disposing)
			{
				SetLayoutManager(null);
				var adapter = this.GetAdapter();
				SetAdapter(null);
				adapter?.Dispose();
				_layoutManager?.Dispose();
				_layoutManager = null;
			}

			base.Dispose(disposing);
		}
	}

	internal class ScrollLayoutManager : LinearLayoutManager
	{
		public ScrollMode ScrollVertically { get; set; } = ScrollMode.Auto;

		public ScrollLayoutManager(Context context, int orientation, bool reverseLayout) : base(context, orientation, reverseLayout)
		{
		}

		int GetVisibleChildCount()
		{
			var firstVisibleIndex = FindFirstCompletelyVisibleItemPosition();
			var lastVisibleIndex = FindLastCompletelyVisibleItemPosition();
			return lastVisibleIndex - firstVisibleIndex + 1;
		}

		public override bool CanScrollVertically()
		{
			switch (ScrollVertically)
			{
				case ScrollMode.Disabled:
					return false;
				case ScrollMode.Enabled:
					return true;
				default:
				case ScrollMode.Auto:
					return ChildCount > GetVisibleChildCount();
			}
		}
	}
}
