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
        private const int LED_PIN = 6;
        private const int BUTTON_PIN = 5;
        private GpioPin ledPin;
        private GpioPin sensorPin;
        private GpioPinValue ledPinValue = GpioPinValue.High;
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);
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
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            sensorPin = gpio.OpenPin(BUTTON_PIN);
            ledPin = gpio.OpenPin(LED_PIN);

            // Initialize LED to the OFF state by first writing a HIGH value
            // We write HIGH because the LED is wired in a active LOW configuration
            ledPin.Write(GpioPinValue.High);
            ledPin.SetDriveMode(GpioPinDriveMode.Output);

            // Check if input pull-up resistors are supported
            if (sensorPin.IsDriveModeSupported(GpioPinDriveMode.InputPullDown))
                sensorPin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            else
                sensorPin.SetDriveMode(GpioPinDriveMode.Input);

            // Set a debounce timeout to filter out switch bounce noise from a button press
            sensorPin.DebounceTimeout = TimeSpan.FromMilliseconds(10);

            // Register for the ValueChanged event so our buttonPin_ValueChanged 
            // function is called when the button is pressed
            sensorPin.ValueChanged += buttonPin_ValueChanged;

            GpioStatus.Text = "GPIO pins initialized correctly.";

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
            Debug.WriteLine("Windspeed: " + windspeed + "km/h");

            //var message = new Message { Name = "Wind", Value = windspeed };
            //var httpClient = new HttpClient();
            //httpClient.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("Basic", Base64.EncodeTo64("user:pass"));
            //var content = new HttpStringContent(JsonConvert.SerializeObject(message));
            //var result = await httpClient.PostAsync(new Uri("http://192.168.178.85/api/queue/windsensor"), content);
            //Debug.WriteLine("Status: "+result.StatusCode);
        }

        private void buttonPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs e)
        {
            // toggle the state of the LED every time the button is pressed
            if (e.Edge == GpioPinEdge.FallingEdge)
            {
                ledPinValue = GpioPinValue.Low;
                counter++;
            }
            else
            {
                ledPinValue = GpioPinValue.High;
            }
            ledPin.Write(ledPinValue);

            // need to invoke UI updates on the UI thread because this event
            // handler gets invoked on a separate thread.
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (e.Edge == GpioPinEdge.FallingEdge)
                {
                    ledEllipse.Fill = (ledPinValue == GpioPinValue.Low) ?
                        redBrush : grayBrush;
                    GpioStatus.Text = "Button Pressed";
                }
                else
                {
                    GpioStatus.Text = "Button Released";
                }
            });
        }

    }
}