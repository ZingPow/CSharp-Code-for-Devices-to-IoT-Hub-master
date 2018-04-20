using System;

using RPDevice.ViewModels;

using Windows.UI.Xaml.Controls;

namespace RPDevice.Views
{
    public sealed partial class MainPage : Page
    {
        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        public MainPage()
        {
            InitializeComponent();
        }
    }
}
