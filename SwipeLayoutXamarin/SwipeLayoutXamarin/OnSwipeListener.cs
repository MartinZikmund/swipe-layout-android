using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace SwipeLayoutXamarin
{
    public interface IOnSwipeListener
    {
        void OnBeginSwipe(SwipeLayout swipeLayout, bool moveToRight);

        void OnSwipeClampReached(SwipeLayout swipeLayout, bool moveToRight);

        void OnLeftStickyEdge(SwipeLayout swipeLayout, bool moveToRight);

        void OnRightStickyEdge(SwipeLayout swipeLayout, bool moveToRight);       
    }
}