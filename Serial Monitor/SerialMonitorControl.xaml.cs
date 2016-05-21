﻿using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Serial_Monitor
{
    public partial class SerialMonitorControl : UserControl
    {
        private SerialPort port;

        private void ConfigurePort()
        {
            port.PortName = ComPorts.SelectedItem.ToString();
            port.BaudRate = Settings.BaudRate;
            port.DataBits = Settings.DataBits;
            port.Handshake = Settings.Handshake;
            port.Parity = Settings.Parity;
            port.StopBits = Settings.StopBits;
            port.ReadTimeout = Settings.ReadTimeout;
            port.WriteTimeout = Settings.WriteTimeout;
        }

        private void ReceiveByte(byte data)
        {
            Output.AppendText(Settings.Encoding.GetString(new byte[] { data }));
            if (AutoscrollCheck.IsChecked == true)
            {
                Output.ScrollToEnd();
            }
        }

        private void ReceiveNewLine()
        {
            Output.Document.ContentEnd.InsertLineBreak();
            if (AutoscrollCheck.IsChecked == true)
            {
                Output.ScrollToEnd();
            }
        }

        private void PrintColorMessage(string message, SolidColorBrush brush, bool withNewLine = false)
        {
            Output.AppendText(message, brush, withNewLine);
            if (AutoscrollCheck.IsChecked == true)
            {
                Output.ScrollToEnd();
            }
        }

        private void PrintErrorMessage(string message, bool withNewLine = false)
        {
            PrintColorMessage(message, Brushes.Red, withNewLine);
        }

        private void PrintWarningMessage(string message, bool withNewLine = false)
        {
            PrintColorMessage(message, Brushes.Yellow, withNewLine);
        }

        private void PrintSuccessMessage(string message, bool withNewLine = false)
        {
            PrintColorMessage(message, Brushes.Green, withNewLine);
        }

        private void PrintProcessMessage(string message, bool withNewLine = false)
        {
            PrintColorMessage(message, Brushes.Aqua, withNewLine);
        }

        private void SerialUpdate(object e, EventArgs s)
        {
            try
            {
                if (port.BytesToRead > 0)
                {
                    byte character = (byte)port.ReadByte();
                    if (character == Settings.ReceiveNewLine[0])
                    {
                        for (int i = 1; i < Settings.ReceiveNewLine.Length; i++)
                        {
                            int newLineCharacter = port.ReadByte();
                            if (newLineCharacter == -1)
                            {
                                ReceiveByte(character);
                                return;
                            }
                            else if ((char)newLineCharacter != Settings.ReceiveNewLine[i])
                            {
                                ReceiveByte(character);
                                ReceiveByte((byte)newLineCharacter);
                                return;
                            }
                        }

                        ReceiveNewLine();
                    }
                    else
                    {
                        ReceiveByte(character);
                    }
                }
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex.Message, true);
            }
        }

        private DispatcherTimer portHandlerTimer;

        public SerialMonitorControl()
        {
            this.InitializeComponent();

            port = new SerialPort();

            portHandlerTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, Application.Current.Dispatcher);
            portHandlerTimer.Tick += SerialUpdate;
        }

        public void Dispose()
        {
            portHandlerTimer.Stop();
            port.Close();
            port.Dispose();
        }

        private void SettingsOutputControl_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (Settings.Visibility == Visibility.Visible)
            {
                Settings.Visibility = Visibility.Collapsed;
                Output.Visibility = Visibility.Visible;
                SettingsOutputControl.Content = "Show Settings";
            }
            else if (Settings.Visibility == Visibility.Collapsed)
            {
                Settings.Visibility = Visibility.Visible;
                Output.Visibility = Visibility.Collapsed;
                SettingsOutputControl.Content = "Show Output";
            }
        }

        private void Connect_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Settings.Visibility = Visibility.Collapsed;
            Output.Visibility = Visibility.Visible;
            SettingsOutputControl.Content = "Show Settings";

            if (ComPorts.SelectedIndex != -1)
            {
                try
                {
                    PrintProcessMessage("Configuring port...");
                    ConfigurePort();

                    PrintProcessMessage("Connecting...");
                    port.Open();

                    if (Settings.DtrEnable == true)
                    {
                        port.DtrEnable = true;
                        port.DiscardInBuffer();
                        Thread.Sleep(1000);
                        port.DtrEnable = false;
                    }

                    ConnectButton.Visibility = Visibility.Collapsed;
                    DisconnectButton.Visibility = Visibility.Visible;
                    ReconnectButton.Visibility = Visibility.Visible;
                    ComPorts.IsEnabled = false;

                    MessageToSend.IsEnabled = true;
                    SendButton.IsEnabled = true;

                    Settings.IsEnabled = false;

                    PrintSuccessMessage("Connected!");
                    portHandlerTimer.Start();
                }
                catch (Exception ex)
                {
                    PrintErrorMessage(ex.Message);
                }
            }
            else
            {
                PrintErrorMessage("COM Port not selected!");
            }
        }

        private void Disconnect_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                PrintProcessMessage("Closing port...", true);

                portHandlerTimer.Stop();

                port.Close();

                ConnectButton.Visibility = Visibility.Visible;
                DisconnectButton.Visibility = Visibility.Collapsed;
                ReconnectButton.Visibility = Visibility.Collapsed;
                ComPorts.IsEnabled = true;

                MessageToSend.IsEnabled = false;
                SendButton.IsEnabled = false;

                Settings.IsEnabled = true;

                PrintSuccessMessage("Port closed!");
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex.Message);
            }
        }

        private void Reconnect_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Disconnect_Click(null, null);
            Connect_Click(null, null);
        }

        private void Clear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Output.Clear();
        }

        private void Send_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                byte[] data = Encoding.Convert(
                    Encoding.Default,
                    Settings.Encoding,
                    Encoding.Default.GetBytes(MessageToSend.Text + Settings.SendNewLine));

                port.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                PrintErrorMessage(ex.Message);
            }
        }

        private void ComPorts_DropDownOpened(object sender, EventArgs e)
        {
            string selectedPort = null;
            if (ComPorts.SelectedIndex != -1)
            {
                selectedPort = ComPorts.SelectedItem.ToString();
            }
            ComPorts.Items.Clear();

            foreach (string portName in SerialPort.GetPortNames())
            {
                ComPorts.Items.Add(portName);
            }

            if (selectedPort != null && ComPorts.Items.Contains(selectedPort))
            {
                ComPorts.SelectedItem = selectedPort;
            }
        }

        private void MessageToSend_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Send_Click(null, null);
                MessageToSend.Text = "";
            }
        }
    }
}