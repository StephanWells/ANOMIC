using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Controls
{
    public partial class PatternIcon
    {
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
    }
}