﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AnnotationTool;

namespace Controls
{
    public partial class OccurrenceIcon
    {
        public event EventHandler DeleteClick;
        public event EventHandler MouseIn;
        public event EventHandler MouseOut;
        public event EventHandler MouseLeftClick;
        public event EventHandler FindSimilar;
        public event EventHandler ConfidenceChange;
        public event EventHandler Edit;

        public OccurrenceIcon()
        {
            InitializeComponent();
        }

        public void UpdateConfidenceGroupName()
        {
            mnuConfidence1.SetCurrentValue(MenuItemExtensions.GroupNameProperty, "confidenceSettings" + PatternNumOfOccurrence + "," + OccurrenceNum);
            mnuConfidence2.SetCurrentValue(MenuItemExtensions.GroupNameProperty, "confidenceSettings" + PatternNumOfOccurrence + "," + OccurrenceNum);
            mnuConfidence3.SetCurrentValue(MenuItemExtensions.GroupNameProperty, "confidenceSettings" + PatternNumOfOccurrence + "," + OccurrenceNum);
        }

        public string OccurrenceText
        {
            get { return (string)GetValue(OccurrenceTextProperty); }
            set { SetValue(OccurrenceTextProperty, value); }
        }

        public static readonly DependencyProperty OccurrenceTextProperty =
        DependencyProperty.Register("OccurrenceText", typeof(string), typeof(OccurrenceIcon), new FrameworkPropertyMetadata("OccurrenceText", FrameworkPropertyMetadataOptions.AffectsRender));

        public int PatternNumOfOccurrence
        {
            get { return (int)GetValue(PatternNumOfOccurrenceProperty); }
            set { SetValue(PatternNumOfOccurrenceProperty, value); }
        }

        public static readonly DependencyProperty PatternNumOfOccurrenceProperty =
        DependencyProperty.Register("PatternNumOfOccurrence", typeof(int), typeof(OccurrenceIcon));

        public int OccurrenceNum
        {
            get { return (int)GetValue(OccurrenceNumProperty); }
            set { SetValue(OccurrenceNumProperty, value); }
        }

        public static readonly DependencyProperty OccurrenceNumProperty =
        DependencyProperty.Register("OccurrenceNum", typeof(int), typeof(OccurrenceIcon));

        public Visibility AutomaticIcon
        {
            get { return (Visibility)GetValue(AutomaticIconProperty); }
            set { SetValue(AutomaticIconProperty, value); }
        }

        public static readonly DependencyProperty AutomaticIconProperty =
        DependencyProperty.Register("AutomaticIcon", typeof(Visibility), typeof(OccurrenceIcon));

        private void OccurrenceIcon_DeleteClick(object sender, RoutedEventArgs e)
        {
            DeleteClick?.Invoke(this, e);
        }

        private void OccurrenceIcon_MouseIn(object sender, RoutedEventArgs e)
        {
            MouseIn?.Invoke(this, e);
        }

        private void OccurrenceIcon_MouseOut(object sender, RoutedEventArgs e)
        {
            MouseOut?.Invoke(this, e);
        }

        private void OccurrenceIcon_MouseLeftClick(object sender, RoutedEventArgs e)
        {
            MouseLeftClick?.Invoke(this, e);
        }

        private void OccurrenceIcon_FindSimilar(object sender, RoutedEventArgs e)
        {
            FindSimilar?.Invoke(this, e);
        }

        private void OccurrenceIcon_ConfidenceChange(object sender, RoutedEventArgs e)
        {
            ConfidenceChange?.Invoke(this, e);
        }

        private void OccurrenceIcon_Edit(object sender, RoutedEventArgs e)
        {
            Edit?.Invoke(this, e);
        }

        public void DisableButtons()
        {
            var template = Template;

            ((Button)template.FindName("DeleteButton", this)).IsEnabled = false;
        }

        public void EnableButtons()
        {
            var template = Template;

            ((Button)template.FindName("DeleteButton", this)).IsEnabled = true;
        }
    }
}