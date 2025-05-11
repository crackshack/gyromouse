using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Notification;
using Avalonia.Threading;
using Serilog;
using WindowsInput;
using WindowsInput.Native;


namespace GetStartedApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        Log.Information("Application started");
    }
    SerialPort serialPort = new SerialPort();
    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (double.TryParse(Celsius.Text, out double C))
        {
            var F = C * (9d / 5d) + 32;
            Fahrenheit.Text = F.ToString("0.0");
        }
        else
        {
            Celsius.Text = "0";
            Fahrenheit.Text = "0";
        }
        DropDown.Items.Clear();
    }

    private async void Refresh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DropDown.Items.Clear();
        //var serport = new SerialPort(DropDown.SelectedItem as string);
        foreach (var item in SerialPort.GetPortNames())
        {
            DropDown.Items.Add(item);
        }


        return;
    }

    private void Connect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (serialPort is null)
            serialPort = new SerialPort();
        if (serialPort.IsOpen)
            return;

        serialPort.PortName = DropDown.SelectedItem as string;
        serialPort.BaudRate = 115200;
        serialPort.DataReceived += SerialPort_DataReceived;


        serialPort.Open();
    }


    bool processing = false;
    InputSimulator simulator = new InputSimulator();
    int numberOfSamples = 0;
    float[] samplesX = new float[1000], samplesY = new float[1000];

    private async void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        var port = sender as SerialPort;


        if (!processing)
        {
            processing = true;


            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (port is null)
                    return;
                if (!port.IsOpen)
                    return;

                var pomosna = port.ReadLine();

                float firstVariable = 0, secondVariable = 0;
                var text = pomosna.Split(',');
                if (text.Length >= 5)
                {
                    float.TryParse(text[3], out firstVariable);

                    float.TryParse(text[5], out secondVariable);
                }
                //za kalibracija na offset
                #region Calibration
                /*if (numberOfSamples < 500)
                {
                    samplesX[numberOfSamples] = x;
                    samplesY[numberOfSamples] = y;
                    numberOfSamples++;
                }
                else
                    SerialOut.Text = $"avgX {(samplesX.Sum() / 500)}  avgY {(samplesY.Sum() / 500)}";
                */
                firstVariable = firstVariable + 3.052f;
                secondVariable = secondVariable + 1.11f;
                #endregion

                #region Deadzone
                if (Math.Abs(firstVariable) < 2)
                    firstVariable = 0;
                if (Math.Abs(secondVariable) < 2)
                    secondVariable = 0;
                #endregion

                #region speed
                firstVariable /= 1.8f;
                secondVariable /= 1.8f;
                #endregion
                simulator.Mouse.MoveMouseBy((int)-secondVariable, (int)firstVariable);

                //    SerialOut.Text = $"{-(int)(y / 5 +0.4)}  {(int)(x/5 +1.15)}";
            });


            processing = false;
        }
        //flush old data
        if (port is null)
            return;
        if (!port.IsOpen)
            return;
        if (port.BytesToRead > 50)
            port.ReadLine();


    }

    private void Discconect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (serialPort is not null)
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
    }

    private async void Button_Click_1(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
       
    }
  
}
