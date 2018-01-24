using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android;
using Android.Animation;
using Android.Content;
using Android.Content.Res;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Java.Lang;
using Java.Util;
using Math = System.Math;
using LayoutParams = SwipeLayoutXamarin.SwipeLayout.LayoutParams;

namespace SwipeLayoutXamarin
{
    public class SwipeLayout : ViewGroup
    {
        internal const string ClassTag = nameof(SwipeLayout);

        private const float DefaultVelocityThreshold = 1500f;

        private const int TouchStateWait = 0;
        private const int TouchStateSwipe = 1;
        private const int TouchStateSkip = 2;

        private readonly IMap _hackedParents = new WeakHashMap();

        private IOnSwipeListener _swipeListener;
        private ViewDragHelper _dragHelper;

        private View _leftView;
        private View _rightView;
        private View _centerView;

        private float _velocityThreshold;
        private float _touchSlop;

        private WeakReference<ObjectAnimator> _resetAnimator;

        private bool _leftSwipeEnabled = true;
        private bool _rightSwipeEnabled = true;

        private int _touchState = TouchStateWait;
        private float _touchX;
        private float _touchY;

        private DragCallbackHandler _dragCallback = null;

        public SwipeLayout(Context context) : base(context)
        {
            Initialize(context, null);
        }

        public SwipeLayout(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Initialize(context, null);
        }

        public SwipeLayout(Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            Initialize(context, attrs);
        }

        private void Initialize(Context context, IAttributeSet attrs)
        {
            _dragCallback = new DragCallbackHandler(this);
            _dragHelper = ViewDragHelper.Create(this, 1f, _dragCallback);
            _velocityThreshold = TypedValue.ApplyDimension(ComplexUnitType.Dip, DefaultVelocityThreshold, Resources.DisplayMetrics);
            _touchSlop = ViewConfiguration.Get(Context).ScaledTouchSlop;

            if (attrs != null)
            {
                TypedArray styledAttributes = context.ObtainStyledAttributes(attrs, Resource.Styleable.SwipeLayout);
                if (styledAttributes.HasValue(Resource.Styleable.SwipeLayout_swipe_enabled))
                {
                    _leftSwipeEnabled = styledAttributes.GetBoolean(Resource.Styleable.SwipeLayout_swipe_enabled, true);
                    _rightSwipeEnabled = styledAttributes.GetBoolean(Resource.Styleable.SwipeLayout_swipe_enabled, true);
                }
                if (styledAttributes.HasValue(Resource.Styleable.SwipeLayout_left_swipe_enabled))
                {
                    _leftSwipeEnabled = styledAttributes.GetBoolean(Resource.Styleable.SwipeLayout_left_swipe_enabled, true);
                }
                if (styledAttributes.HasValue(Resource.Styleable.SwipeLayout_right_swipe_enabled))
                {
                    _rightSwipeEnabled = styledAttributes.GetBoolean(Resource.Styleable.SwipeLayout_right_swipe_enabled, true);
                }

                styledAttributes.Recycle();
            }
        }

        public void SetOnSwipeListener(IOnSwipeListener swipeListener)
        {
            _swipeListener = swipeListener;
        }

        /**
         * reset swipe-layout state to initial position
         */
        public void Reset()
        {
            if (_centerView == null) return;

            FinishResetAnimator();
            _dragHelper.Abort();

            OffsetChildren(null, _centerView.Left);
        }

        /**
         * reset swipe-layout state to initial position with animation (200ms)
         */
        public void AnimateReset()
        {
            if (_centerView == null) return;

            FinishResetAnimator();
            _dragHelper.Abort();

            ObjectAnimator animator = new ObjectAnimator();
            animator.SetTarget(this);
            animator.PropertyName = "offset";
            animator.SetInterpolator(new AccelerateInterpolator());
            animator.SetIntValues(_centerView.Left, 0);
            animator.SetDuration(200);
            animator.Start();
            _resetAnimator = new WeakReference<ObjectAnimator>(animator);
        }

        private void FinishResetAnimator()
        {
            if (_resetAnimator == null) return;

            if (_resetAnimator.TryGetTarget(out var animator))
            {
                _resetAnimator.SetTarget(null);
                if (animator.IsRunning)
                {
                    animator.End();
                }
            }
        }

        /**
         * get horizontal offset from initial position
         */
        public int GetOffset()
        {
            return _centerView?.Left ?? 0;
        }

        /**
         * set horizontal offset from initial position
         */
        public void SetOffset(int offset)
        {
            if (_centerView != null)
            {
                OffsetChildren(null, offset - _centerView.Left);
            }
        }

        public bool IsSwipeEnabled()
        {
            return _leftSwipeEnabled || _rightSwipeEnabled;
        }

        public bool IsLeftSwipeEnabled()
        {
            return _leftSwipeEnabled;
        }

        public bool IsRightSwipeEnabled()
        {
            return _rightSwipeEnabled;
        }

        /**
         * enable or disable swipe gesture handling
         *
         * @param enabled
         */
        public void SetSwipeEnabled(bool enabled)
        {
            _leftSwipeEnabled = enabled;
            _rightSwipeEnabled = enabled;
        }

        /**
         * Enable or disable swipe gesture from left side
         *
         * @param _leftSwipeEnabled
         */

        public void SetLeftSwipeEnabled(bool leftSwipeEnabled)
        {
            _leftSwipeEnabled = leftSwipeEnabled;
        }

        /**
         * Enable or disable swipe gesture from right side
         *
         * @param rightSwipeEnabled
         */

        public void SetRightSwipeEnabled(bool rightSwipeEnabled)
        {
            _rightSwipeEnabled = rightSwipeEnabled;
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            int count = ChildCount;

            int maxHeight = 0;

            // Find out how big everyone wants to be
            if (MeasureSpec.GetMode(heightMeasureSpec) == MeasureSpecMode.Exactly)
            {
                MeasureChildren(widthMeasureSpec, heightMeasureSpec);
            }
            else
            {
                //find a child with biggest height
                for (int i = 0; i < count; i++)
                {
                    View child = GetChildAt(i);
                    MeasureChild(child, widthMeasureSpec, heightMeasureSpec);
                    maxHeight = Math.Max(maxHeight, child.MeasuredHeight);
                }

                if (maxHeight > 0)
                {
                    heightMeasureSpec = MeasureSpec.MakeMeasureSpec(maxHeight, MeasureSpecMode.Exactly);
                    MeasureChildren(widthMeasureSpec, heightMeasureSpec);
                }
            }

            // Find rightmost and bottom-most child
            for (int i = 0; i < count; i++)
            {
                View child = GetChildAt(i);
                if (child.Visibility != ViewStates.Gone)
                {
                    int childBottom = child.MeasuredHeight;
                    maxHeight = Math.Max(maxHeight, childBottom);
                }
            }

            maxHeight += PaddingTop + PaddingBottom;
            maxHeight = Math.Max(maxHeight, SuggestedMinimumHeight);

            SetMeasuredDimension(ResolveSize(SuggestedMinimumWidth, widthMeasureSpec),
                    ResolveSize(maxHeight, heightMeasureSpec));
        }

        protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
        {
            LayoutChildren(left, top, right, bottom);
        }

        private void LayoutChildren(int left, int top, int right, int bottom)
        {
            int count = ChildCount;

            int parentTop = PaddingTop;

            for (int i = 0; i < count; i++)
            {
                View child = GetChildAt(i);
                SwipeLayout.LayoutParams lp = (SwipeLayout.LayoutParams)child.LayoutParameters;                
                switch (lp.Gravity)
                {
                    case LayoutParams.Center:
                        _centerView = child;
                        break;

                    case LayoutParams.Left:
                        _leftView = child;
                        break;

                    case LayoutParams.Right:
                        _rightView = child;
                        break;
                }
            }

            if (_centerView == null) throw new RuntimeException("Center view must be added");

            for (int i = 0; i < count; i++)
            {
                View child = GetChildAt(i);
                if (child.Visibility != ViewStates.Gone)
                {
                    LayoutParams lp = (LayoutParams)child.LayoutParameters;

                    int width = child.MeasuredWidth;
                    int height = child.MeasuredHeight;

                    int childLeft;
                    int childTop;

                    int orientation = lp.Gravity;

                    switch (orientation)
                    {
                        case LayoutParams.Left:
                            childLeft = _centerView.Left - width;
                            break;

                        case LayoutParams.Right:
                            childLeft = _centerView.Right;
                            break;

                        case LayoutParams.Center:
                        default:
                            childLeft = child.Left;
                            break;
                    }
                    childTop = parentTop;

                    child.Layout(childLeft, childTop, childLeft + width, childTop + height);
                }
            }
        }



        private void StartScrollAnimation(View view, int targetX, bool moveToClamp, bool toRight)
        {
            if (_dragHelper.SettleCapturedViewAt(targetX, view.Top))
            {
                ViewCompat.PostOnAnimation(view, new SettleRunnable(this, view, moveToClamp, toRight));
            }
            else
            {
                if (moveToClamp && _swipeListener != null)
                {
                    _swipeListener.OnSwipeClampReached(this, toRight);
                }
            }
        }

        private LayoutParams GetLayoutParams(View view)
        {
            return (LayoutParams)view.LayoutParameters;
        }

        private void OffsetChildren(View skip, int dx)
        {
            if (dx == 0) return;

            int count = ChildCount;
            for (int i = 0; i < count; i++)
            {
                View child = GetChildAt(i);
                if (child == skip) continue;

                child.OffsetLeftAndRight(dx);
                Invalidate(child.Left, child.Top, child.Right, child.Bottom);
            }
        }

        private void HackParents()
        {
            IViewParent parent = Parent;
            while (parent != null)
            {
                if (parent is INestedScrollingParent)
                {
                    View view = (View)parent;
                    _hackedParents.Put(view, view.Enabled);
                }
                parent = parent.Parent;
            }
        }

        private void UnHackParents()
        {
            foreach (IMapEntry entry in _hackedParents.EntrySet())
            {
                View view = ( View )entry.Key;
                if (view != null)
                {
                    view.Enabled = ( bool )entry.Value;
                }
            }
            _hackedParents.Clear();
        }

        public override bool OnInterceptTouchEvent(MotionEvent e)
        {
            return IsSwipeEnabled()
                ? _dragHelper.ShouldInterceptTouchEvent(e)
                : base.OnInterceptTouchEvent(e);
        }


        public override bool OnTouchEvent(MotionEvent e)
        {
            bool defaultResult = base.OnTouchEvent(e);
            if (!IsSwipeEnabled())
            {
                return defaultResult;
            }

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    _touchState = TouchStateWait;
                    _touchX = e.GetX();
                    _touchY = e.GetY();
                    break;
                case MotionEventActions.Move:
                    if (_touchState == TouchStateWait)
                    {
                        float dx = Math.Abs(e.GetX() - _touchX);
                        float dy = Math.Abs(e.GetY() - _touchY);

                        bool isLeftToRight = (e.GetX() - _touchX) > 0;

                        if ((isLeftToRight && !_leftSwipeEnabled) || (!isLeftToRight && !_rightSwipeEnabled))
                        {
                            return defaultResult;
                        }

                        if (dx >= _touchSlop || dy >= _touchSlop)
                        {
                            _touchState = dy == 0 || dx / dy > 1f ? TouchStateSwipe : TouchStateSkip;
                            if (_touchState == TouchStateSwipe)
                            {
                                RequestDisallowInterceptTouchEvent(true);
                                HackParents();

                                _swipeListener?.OnBeginSwipe(this, e.GetX() > _touchX);
                            }
                        }
                    }
                    break;
                case MotionEventActions.Cancel:
                case MotionEventActions.Up:
                    if (_touchState == TouchStateSwipe)
                    {
                        UnHackParents();
                        RequestDisallowInterceptTouchEvent(false);
                    }
                    _touchState = TouchStateWait;
                    break;
            }

            if (e.ActionMasked != MotionEventActions.Move || _touchState == TouchStateSwipe)
            {
                _dragHelper.ProcessTouchEvent(e);
            }

            return true;
        }


        protected override ViewGroup.LayoutParams GenerateDefaultLayoutParams()
        {
            return new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        }

        public override ViewGroup.LayoutParams GenerateLayoutParams(IAttributeSet attrs)
        {
            return new LayoutParams(Context, attrs);
        }

        protected override ViewGroup.LayoutParams GenerateLayoutParams(ViewGroup.LayoutParams p)
        {
            return new LayoutParams(p);
        }
        
        protected override bool CheckLayoutParams(ViewGroup.LayoutParams p)
        {
            return p is LayoutParams;
        }

        private class SettleRunnable : Java.Lang.Object, Java.Lang.IRunnable
        {
            private readonly SwipeLayout _swipeLayout;
            private readonly View _view;
            private readonly bool _moveToClamp;
            private readonly bool _moveToRight;

            internal SettleRunnable(SwipeLayout swipeLayout, View view, bool moveToClamp, bool moveToRight) 
            {
                _swipeLayout = swipeLayout;
                _view = view;
                _moveToClamp = moveToClamp;
                _moveToRight = moveToRight;
            }

            public void Run()
            {
                if (_swipeLayout._dragHelper != null && _swipeLayout._dragHelper.ContinueSettling(true))
                {
                    ViewCompat.PostOnAnimation(_view, this);
                }
                else
                {
                    Log.Debug(SwipeLayout.ClassTag, "ONSWIPE Clamp: " + _moveToClamp + " ; moveToRight: " + _moveToRight);
                    if (_moveToClamp)
                    {
                        _swipeLayout._swipeListener?.OnSwipeClampReached(_swipeLayout, _moveToRight);
                    }
                }
            }
        }

        internal class DragCallbackHandler : ViewDragHelper.Callback
        {
            private readonly SwipeLayout _swipeLayout;

            private int _initLeft;

            public DragCallbackHandler(SwipeLayout swipeLayout)
            {
                _swipeLayout = swipeLayout;
            }

            public override bool TryCaptureView(View child, int pointerId)
            {
                _initLeft = child.Left;
                return true;
            }

            public override int ClampViewPositionHorizontal(View child, int left, int dx)
            {
                if (dx > 0)
                {
                    return ClampMoveRight(child, left);
                }
                else
                {
                    return ClampMoveLeft(child, left);
                }
            }

            public override int GetViewHorizontalDragRange(View child)
            {
                return _swipeLayout.Width;
            }

            public override void OnViewReleased(View releasedChild, float xvel, float yvel)
            {
                Log.Debug(SwipeLayout.ClassTag, "VELOCITY " + xvel + "; THRESHOLD " + _swipeLayout._velocityThreshold);

                int dx = releasedChild.Left - _initLeft;
                if (dx == 0) return;


                bool handled = false;
                if (dx > 0)
                {

                    handled = xvel >= 0 ? OnMoveRightReleased(releasedChild, dx, xvel) : OnMoveLeftReleased(releasedChild, dx, xvel);

                }
                else if (dx < 0)
                {

                    handled = xvel <= 0 ? OnMoveLeftReleased(releasedChild, dx, xvel) : OnMoveRightReleased(releasedChild, dx, xvel);
                }

                if (!handled)
                {
                    _swipeLayout.StartScrollAnimation(releasedChild, releasedChild.Left - _swipeLayout._centerView.Left, false, dx > 0);
                }
            }

            private bool LeftViewClampReached(LayoutParams leftViewLP)
            {
                if (_swipeLayout._leftView == null) return false;

                switch (leftViewLP.Clamp)
                {
                    case LayoutParams.ClampParent:
                        return _swipeLayout._leftView.Right >= _swipeLayout.Width;

                    case LayoutParams.ClampSelf:
                        return _swipeLayout._leftView.Right >= _swipeLayout._leftView.Width;

                    default:
                        return _swipeLayout._leftView.Right >= leftViewLP.Clamp;
                }
            }

            private bool rightViewClampReached(LayoutParams lp)
            {
                if (_swipeLayout._rightView == null) return false;

                switch (lp.Clamp)
                {
                    case LayoutParams.ClampParent:
                        return _swipeLayout._rightView.Right <= _swipeLayout.Width;

                    case LayoutParams.ClampSelf:
                        return _swipeLayout._rightView.Right <= _swipeLayout.Width;

                    default:
                        return _swipeLayout._rightView.Left + lp.Clamp <= _swipeLayout.Width;
                }
            }

            public override void OnViewPositionChanged(View changedView, int left, int top, int dx, int dy)
            {
                _swipeLayout.OffsetChildren(changedView, dx);

                if (_swipeLayout._swipeListener == null) return;

                int stickyBound;
                if (dx > 0)
                {
                    //move to right

                    if (_swipeLayout._leftView != null)
                    {
                        stickyBound = GetStickyBound(_swipeLayout._leftView);
                        if (stickyBound != LayoutParams.StickyNone)
                        {
                            if (_swipeLayout._leftView.Right - stickyBound > 0 && _swipeLayout._leftView.Right - stickyBound - dx <= 0)
                                _swipeLayout._swipeListener.OnLeftStickyEdge(_swipeLayout, true);
                        }
                    }

                    if (_swipeLayout._rightView != null)
                    {
                        stickyBound = GetStickyBound(_swipeLayout._rightView);
                        if (stickyBound != LayoutParams.StickyNone)
                        {
                            if (_swipeLayout._rightView.Left + stickyBound > _swipeLayout.Width && _swipeLayout._rightView.Left + stickyBound - dx <= _swipeLayout.Width)
                                _swipeLayout._swipeListener.OnRightStickyEdge(_swipeLayout, true);
                        }
                    }
                }
                else if (dx < 0)
                {
                    //move to left

                    if (_swipeLayout._leftView != null)
                    {
                        stickyBound = GetStickyBound(_swipeLayout._leftView);
                        if (stickyBound != LayoutParams.StickyNone)
                        {
                            if (_swipeLayout._leftView.Right - stickyBound <= 0 && _swipeLayout._leftView.Right - stickyBound - dx > 0)
                                _swipeLayout._swipeListener.OnLeftStickyEdge(_swipeLayout, false);
                        }
                    }

                    if (_swipeLayout._rightView != null)
                    {
                        stickyBound = GetStickyBound(_swipeLayout._rightView);
                        if (stickyBound != LayoutParams.StickyNone)
                        {
                            if (_swipeLayout._rightView.Left + stickyBound <= _swipeLayout.Width && _swipeLayout._rightView.Left + stickyBound - dx > _swipeLayout.Width)
                                _swipeLayout._swipeListener.OnRightStickyEdge(_swipeLayout, false);
                        }
                    }
                }
            }

            private int GetStickyBound(View view)
            {
                LayoutParams lp = _swipeLayout.GetLayoutParams(view);
                if (lp.Sticky == LayoutParams.StickyNone) return LayoutParams.StickyNone;

                return lp.Sticky == LayoutParams.StickySelf ? view.Width : lp.Sticky;
            }

            private int ClampMoveRight(View child, int left)
            {
                if (_swipeLayout._leftView == null)
                {
                    return child == _swipeLayout._centerView ? Math.Min(left, 0) : Math.Min(left, _swipeLayout.Width);
                }

                LayoutParams lp = _swipeLayout.GetLayoutParams(_swipeLayout._leftView);
                switch (lp.Clamp)
                {
                    case LayoutParams.ClampParent:
                        return Math.Min(left, _swipeLayout.Width + child.Left - _swipeLayout._leftView.Right);

                    case LayoutParams.ClampSelf:
                        return Math.Min(left, child.Left - _swipeLayout._leftView.Left);

                    default:
                        return Math.Min(left, child.Left - _swipeLayout._leftView.Right + lp.Clamp);
                }
            }

            private int ClampMoveLeft(View child, int left)
            {
                if (_swipeLayout._rightView == null)
                {
                    return child == _swipeLayout._centerView ? Math.Max(left, 0) : Math.Max(left, -child.Width);
                }

                LayoutParams lp = _swipeLayout.GetLayoutParams(_swipeLayout._rightView);
                switch (lp.Clamp)
                {
                    case LayoutParams.ClampParent:
                        return Math.Max(child.Left - _swipeLayout._rightView.Left, left);

                    case LayoutParams.ClampSelf:
                        return Math.Max(left, _swipeLayout.Width - _swipeLayout._rightView.Left + child.Left - _swipeLayout._rightView.Width);

                    default:
                        return Math.Max(left, _swipeLayout.Width - _swipeLayout._rightView.Left + child.Left - lp.Clamp);
                }
            }

            private bool OnMoveRightReleased(View child, int dx, float xvel)
            {

                if (xvel > _swipeLayout._velocityThreshold)
                {
                    int left = _swipeLayout._centerView.Left < 0 ? child.Left - _swipeLayout._centerView.Left : _swipeLayout.Width;
                    bool moveToOriginal = _swipeLayout._centerView.Left < 0;
                    _swipeLayout.StartScrollAnimation(child, ClampMoveRight(child, left), !moveToOriginal, true);
                    return true;
                }

                if (_swipeLayout._leftView == null)
                {
                    _swipeLayout.StartScrollAnimation(child, child.Left - _swipeLayout._centerView.Left, false, true);
                    return true;
                }

                LayoutParams lp = _swipeLayout.GetLayoutParams(_swipeLayout._leftView);

                if (dx > 0 && xvel >= 0 && LeftViewClampReached(lp))
                {
                    if (_swipeLayout != null)
                    {
                        _swipeLayout._swipeListener.OnSwipeClampReached(_swipeLayout, true);
                    }
                    return true;
                }

                if (dx > 0 && xvel >= 0 && lp.BringToClamp != LayoutParams.BringToClampNo && _swipeLayout._leftView.Right > lp.BringToClamp)
                {
                    int left = _swipeLayout._centerView.Left < 0 ? child.Left - _swipeLayout._centerView.Left : _swipeLayout.Width;
                    _swipeLayout.StartScrollAnimation(child, ClampMoveRight(child, left), true, true);
                    return true;
                }

                if (lp.Sticky != LayoutParams.StickyNone)
                {
                    int stickyBound = lp.Sticky == LayoutParams.StickySelf ? _swipeLayout._leftView.Width : lp.Sticky;
                    float amplitude = stickyBound * lp.StickySensitivity;

                    if (IsBetween(-amplitude, amplitude, _swipeLayout._centerView.Left - stickyBound))
                    {
                        bool toClamp = (lp.Clamp == LayoutParams.ClampSelf && stickyBound == _swipeLayout._leftView.Width) ||
                                lp.Clamp == stickyBound ||
                                (lp.Clamp == LayoutParams.ClampParent && stickyBound == _swipeLayout.Width);
                        _swipeLayout.StartScrollAnimation(child, child.Left - _swipeLayout._centerView.Left + stickyBound, toClamp, true);
                        return true;
                    }
                }
                return false;
            }

            private bool OnMoveLeftReleased(View child, int dx, float xvel)
            {
                if (-xvel > _swipeLayout._velocityThreshold)
                {
                    int left = _swipeLayout._centerView.Left > 0 ? child.Left - _swipeLayout._centerView.Left : -_swipeLayout.Width;
                    bool moveToOriginal = _swipeLayout._centerView.Left > 0;
                    _swipeLayout.StartScrollAnimation(child, ClampMoveRight(child, left), !moveToOriginal, false);
                    return true;
                }

                if (_swipeLayout._rightView == null)
                {
                    _swipeLayout.StartScrollAnimation(child, child.Left - _swipeLayout._centerView.Left, false, false);
                    return true;
                }


                LayoutParams lp = _swipeLayout.GetLayoutParams(_swipeLayout._rightView);

                if (dx < 0 && xvel <= 0 && rightViewClampReached(lp))
                {
                    if (_swipeLayout._swipeListener != null)
                    {
                        _swipeLayout._swipeListener.OnSwipeClampReached(_swipeLayout, false);
                    }
                    return true;
                }

                if (dx < 0 && xvel <= 0 && lp.BringToClamp != LayoutParams.BringToClampNo && _swipeLayout._rightView.Left + lp.BringToClamp < _swipeLayout.Width)
                {
                    int left = _swipeLayout._centerView.Left > 0 ? child.Left - _swipeLayout._centerView.Left : -_swipeLayout.Width;
                    _swipeLayout.StartScrollAnimation(child, ClampMoveLeft(child, left), true, false);
                    return true;
                }

                if (lp.Sticky != LayoutParams.StickyNone)
                {
                    int stickyBound = lp.Sticky == LayoutParams.StickySelf ? _swipeLayout._rightView.Width : lp.Sticky;
                    float amplitude = stickyBound * lp.StickySensitivity;

                    if (IsBetween(-amplitude, amplitude, _swipeLayout._centerView.Right + stickyBound - _swipeLayout.Width))
                    {
                        bool toClamp = (lp.Clamp == LayoutParams.ClampSelf && stickyBound == _swipeLayout._rightView.Width) ||
                                lp.Clamp == stickyBound ||
                                (lp.Clamp == LayoutParams.ClampParent && stickyBound == _swipeLayout.Width);
                        _swipeLayout.StartScrollAnimation(child, child.Left - _swipeLayout._rightView.Left + _swipeLayout.Width - stickyBound, toClamp, false);
                        return true;
                    }
                }

                return false;
            }

            private bool IsBetween(float left, float right, float check)
            {
                return check >= left && check <= right;
            }
        }

        public new class LayoutParams : ViewGroup.LayoutParams
        {
            public const int Left = -1;
            public const int Right = 1;
            public const int Center = 0;

            public const int ClampParent = -1;
            public const int ClampSelf = -2;
            public const int BringToClampNo = -1;

            public const int StickySelf = -1;
            public const int StickyNone = -2;
            private const float DefaultStickySensitivity = 0.9f;

            public int Gravity { get; set; } = Center;
            public int Sticky { get; set; }
            public float StickySensitivity { get; set; } = DefaultStickySensitivity;
            public int Clamp { get; set; } = ClampSelf;
            public int BringToClamp { get; set; } = BringToClampNo;

            public LayoutParams(Context c, IAttributeSet attrs) : base(c, attrs)
            {
                TypedArray a = c.ObtainStyledAttributes(attrs, Resource.Styleable.SwipeLayout);

                int N = a.IndexCount;
                for (int i = 0; i < N; ++i)
                {
                    int attr = a.GetIndex(i);
                    if (attr == Resource.Styleable.SwipeLayout_gravity)
                    {
                        Gravity = a.GetInt(attr, Center);
                    }
                    else if (attr == Resource.Styleable.SwipeLayout_sticky)
                    {
                        Sticky = a.GetLayoutDimension(attr, StickySelf);
                    }
                    else if (attr == Resource.Styleable.SwipeLayout_clamp)
                    {
                        Clamp = a.GetLayoutDimension(attr, ClampSelf);
                    }
                    else if (attr == Resource.Styleable.SwipeLayout_bring_to_clamp)
                    {
                        BringToClamp = a.GetLayoutDimension(attr, BringToClampNo);
                    }
                    else if (attr == Resource.Styleable.SwipeLayout_sticky_sensitivity)
                    {
                        StickySensitivity = a.GetFloat(attr, DefaultStickySensitivity);
                    }
                }
                a.Recycle();
            }

            public LayoutParams(ViewGroup.LayoutParams source) : base(source)
            {
            }

            public LayoutParams(int width, int height) : base(width, height)
            {
            }
        }
    }

    


}
