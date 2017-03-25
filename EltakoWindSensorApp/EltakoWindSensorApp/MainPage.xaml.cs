// Copyright (c) Microsoft. All rights reserved.

using EltakoWindSensorApp.Utils;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Threading;
using Windows.Devices.Gpio;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;

namespace EltakoWindSensorApp
{
    public sealed partial class MainPage : Page
    {
        private const int BUTTON_PIN = 5;
        private GpioPin sensorPin;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush greenBrush = new SolidColorBrush(Windows.UI.Colors.Green);
        private Timer windsensorTimer;

        public MainPage()
        {
            InitializeComponent();
            InitGPIO();
        }

        private volatile int counter = 0;

        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                WindStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            sensorPin = gpio.OpenPin(BUTTON_PIN);

            // Check if input pull-up resistors are supported
            if (sensorPin.IsDriveModeSupported(GpioPinDriveMode.InputPullDown))
                sensorPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                sensorPin.SetDriveMode(GpioPinDriveMode.Input);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            sensorPin.DebounceTimeout = TimeSpan.FromMilliseconds(10);

            // Register for the ValueChanged event so our buttonPin_ValueChanged 
            // function is called when the button is pressed
            sensorPin.ValueChanged += windsensorTriggered;

            WindStatus.Text = "GPIO pins initialized correctly.";

            // calculate windspeed every second
            windsensorTimer = new Timer(windsensorTimerCallback, null, 1000, 1000);
        }

        private async void windsensorTimerCallback(object state) // this timer is called every 1000ms
        {
            var current = counter*2; // two signals per round
            counter = 0;
            // formula from: http://zieren.de/ip-anemometer/
            var windspeed = 1.761 / (1 + current) + 3.013 * current; 
            if (windspeed<=1.761)
            {
                windspeed = 0;
            }
            Debug.WriteLine("Wind speed " + windspeed + " km/h");
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                WindStatus.Text = "Wind speed " + windspeed + " km/h";
            });
        }

        private void windsensorTriggered(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            // toggle the wind sensor rpm counter
            if (e.Edge == GpioPinEdge.FallingEdge)
            {
                counter++;
            }
        }

    }
}