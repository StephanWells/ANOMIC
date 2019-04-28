using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Controls
{
    public partial class PatternIcon
    {
        public event EventHandler DeleteClick;
        public event EventHandler ViewToggle;
        public event EventHandler ViewSolo;
        public event EventHandler CollExpToggle;

        public PatternIcon()
        {
            InitializeComponent();
        }

        public string TextContent
        {
            get { return (string)GetValue(TextContentProperty); }
            set { SetValue(TextContentProperty, value); }
        }

        public static readonly DependencyProperty TextContentProperty =
        DependencyProperty.Register("TextContent", typeof(string), typeof(PatternIcon), new FrameworkPropertyMetadata("TextContent", FrameworkPropertyMetadataOptions.AffectsRender));

        public bool CollExp
        {
            get { return (bool)GetValue(CollExpProperty); }
            set { SetValue(CollExpProperty, value); }
        }

        public static readonly DependencyProperty CollExpProperty =
        DependencyProperty.Register("CollExp", typeof(bool), typeof(PatternIcon));

        public bool View
        {
            get { return (bool)GetValue(ViewProperty); }
            set { SetValue(ViewProperty, value); }
        }

        public static readonly DependencyProperty ViewProperty =
        DependencyProperty.Register("View", typeof(bool), typeof(PatternIcon));

        public int PatternNum
        {
            get { return (int)GetValue(PatternNumProperty); }
            set { SetValue(PatternNumProperty, value); }
        }

        public static readonly DependencyProperty PatternNumProperty =
        DependencyProperty.Register("PatternNum", typeof(int), typeof(PatternIcon));

        private void PatternIcon_DeleteClick(object sender, RoutedEventArgs e)
        {
            DeleteClick?.Invoke(this, e);
        }

        private void PatternIcon_ViewToggle(object sender, RoutedEventArgs e)
        {
            ViewToggle?.Invoke(this, e);
        }

        private void PatternIcon_ViewSolo(object sender, RoutedEventArgs e)
        {
            ViewSolo?.Invoke(this, e);
        }

        private void PatternIcon_CollExpToggle(object sender, RoutedEventArgs e)
        {
            CollExpToggle?.Invoke(this, e);
        }
    }
}