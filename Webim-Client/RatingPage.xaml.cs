using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using WebimSDK;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers.Provider;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkID=390556

namespace Webim_Client
{
    public struct OperatorRateResult
    {
        public WMBaseSession.WMOperatorRate rate;
        public bool Canceled;
    }

    public sealed partial class RatingPage : ContentDialog
    {

        public OperatorRateResult Result = new OperatorRateResult();

        public RatingPage()
        {
            this.InitializeComponent();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            this.Result.Canceled = true;
        }

        private OperatorRateResult RadioButtonNameToRate(string name)
        {
            WMBaseSession.WMOperatorRate value = WMBaseSession.WMOperatorRate.WMOperatorRateFiveStars;
            switch (name)
            {
                case "one": value = WMBaseSession.WMOperatorRate.WMOperatorRateOneStar; break;
                case "two": value = WMBaseSession.WMOperatorRate.WMOperatorRateTwoStars; break;
                case "three": value = WMBaseSession.WMOperatorRate.WMOperatorRateThreeStars; break;
                case "four": value = WMBaseSession.WMOperatorRate.WMOperatorRateFourStars; break;
                case "five": value = WMBaseSession.WMOperatorRate.WMOperatorRateFiveStars; break;
            }
            return new OperatorRateResult() {
                rate = value,
                Canceled = false,
            };
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            Result = RadioButtonNameToRate(rb.Name);
        }
    }
}
