using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using NAudio.Wave;

namespace VoiceOverLAN
{
    public partial class MainWindow : Window
    {
        List<Contact> contacts = new List<Contact>();
        VoIPClient VoIPClient = new VoIPClient();
        VoIPListener VoIPListener = new VoIPListener();
        Task listenerTask;
        System.Timers.Timer timer;
        private bool availableStatus = true;
        public static bool isMuted = false;
        public MainWindow()
        {
            InitializeComponent();
            displayIpAddress();
            contacts = readFromJson();
            FillContacts();
            timer = new System.Timers.Timer(1000);
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
            minimizeButton.Click += minimizeWindow;
            closeButton.Click += closeWindow;
            titleBar.MouseDown += dragWindow;
            saveContactButton.Click += saveContact;
            deleteContactButton.Click += deleteContact;
            callButton.Click += makeCall;
            contactListBox.SelectionChanged += fillTextBoxesWithSelectedContact;
            clearInputButton.Click += clearTextBoxes;
            refreshContactButton.Click += refreshContacts;
            listenerTask = Task.Run(() => udpListener());
            BrbBox.SelectionChanged += StatusChanged;
            muteMicButton.Click += muteMic;
        }

        private void muteMic(object sender, RoutedEventArgs e)
        {
           isMuted = !isMuted;
        }

        private void StatusChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BrbBox.SelectedIndex == 0) availableStatus = true;
            else availableStatus = false;
        }

        private void refreshContacts(object sender, RoutedEventArgs e)
        {
            FillContacts();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach(var contact in contacts)
            {
                VoIPListener.SendCode("20", contact.IpAddress);
            }
        }

        private void StartCall(string ip, CancellationToken cancellationToken, CancellationTokenSource cancelToken)
        {
            Task.Run(async () => VoIPClient.ListenVOIP(cancellationToken));
            VoIPClient.SendVOIP(ip, cancellationToken);
            volumeSlider.ValueChanged += (o, arg) => { VoIPClient.ChangeVolume((float)arg.NewValue); };
            endCallButton.Click += (o, arg) => { VoIPListener.SendCode("13", ip); cancelToken.Cancel(); this.Dispatcher.Invoke(() => callSectionStackPanel.Visibility = Visibility.Hidden); };
        }

        private void udpListener()
        {
            //10 - calling
            //11 - call acccepted
            //12 - call refused
            //13 - end call
            //20 - checking availability
            //21 - available <-- response
            //22 - brb <-- response
            int port = 11001;
            var udpClient = new UdpClient(port);
            var ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            CancellationTokenSource cancelToken = new CancellationTokenSource();
            CancellationToken token = cancelToken.Token;

            try
            {
                
                bool timeouted = false;
                while (true)
                {
                    var bytes = udpClient.Receive(ref ipEndPoint);
                    switch (Encoding.UTF8.GetString(bytes, 0, bytes.Length))
                    {
                        case "10":
                            {
                                var temp = contacts.FirstOrDefault(c => c.IpAddress == ipEndPoint.Address.ToString());
                                string dialogString;
                                if (temp != null) dialogString = temp.Nickname;
                                else dialogString = ipEndPoint.Address.ToString();
                                dialogString += " is calling. Do you want to answer?";

                                var reader = new Mp3FileReader(@"Assets\ringtone.mp3");
                                var WaveOut = new WaveOut();
                                reader.Skip(27);
                                WaveOut.Init(reader);
                                WaveOut.Volume = 0.7f;
                                WaveOut.Play();

                                timer = new System.Timers.Timer(20000);
                                timer.AutoReset = false;
                                timer.Enabled = true;
                                timer.Elapsed += (o, arg) => { VoIPListener.SendCode("12", ipEndPoint.Address.ToString()); timeouted = true; timer.Stop(); WaveOut.Stop(); };
                                timer.Start();

                                var result = MessageBox.Show(dialogString, "Incoming call", MessageBoxButton.YesNo);

                                timer.Dispose();
                                WaveOut.Stop();

                                if (timeouted == false && result == MessageBoxResult.Yes)
                                {
                                    this.Dispatcher.Invoke(() => callSectionStackPanel.Visibility = Visibility.Visible);
                                    VoIPListener.SendCode("11", ipEndPoint.Address.ToString());

                                    Task.Run(async () => StartCall(ipEndPoint.Address.ToString(), token, cancelToken), cancelToken.Token);
                                }
                                else if (timeouted == false && result == MessageBoxResult.No)
                                {
                                    VoIPListener.SendCode("12", ipEndPoint.Address.ToString());
                                }
                                else
                                {
                                    timeouted = false;
                                }
                                break;
                            }
                        case "11":
                            {
                                Task.Run(async () => StartCall(ipEndPoint.Address.ToString(), token, cancelToken), cancelToken.Token);
                                this.Dispatcher.Invoke(() => callSectionStackPanel.Visibility = Visibility.Visible);
                                break;
                            }
                        case "12":
                            {
                                MessageBox.Show("Call was rejected or recipient didn't response in specified time.");
                                break;
                            }
                        case "13":
                            {
                                cancelToken.Cancel();
                                this.Dispatcher.Invoke(() => callSectionStackPanel.Visibility = Visibility.Hidden);
                                break;
                            }
                        case "20":
                            {
                                if (availableStatus) VoIPListener.SendCode("21", ipEndPoint.Address.ToString());
                                else VoIPListener.SendCode("22", ipEndPoint.Address.ToString());
                                break;
                            }
                        case "21":
                            {
                                var contact = contacts.Find(c => c.IpAddress == ipEndPoint.Address.ToString());
                                contact.Status = Status.Active;
                                break;
                            }
                        case "22":
                            {
                                var contact = contacts.Find(c => c.IpAddress == ipEndPoint.Address.ToString());
                                contact.Status = Status.Brb;
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                udpClient.Close();
            }
        }

        private void clearTextBoxes(object sender, RoutedEventArgs e)
        {
            ipTextBox.Text = "";
            nicknameTextBox.Text = "";
            contactListBox.SelectedIndex = -1;
        }

        private void fillTextBoxesWithSelectedContact(object sender, SelectionChangedEventArgs e)
        {
            if (contactListBox.SelectedIndex != -1)
            {
                ipTextBox.Text = contacts[contactListBox.SelectedIndex].IpAddress;
                nicknameTextBox.Text = contacts[contactListBox.SelectedIndex].Nickname;
            }
        }

        private void makeCall(object sender, RoutedEventArgs e)
        {
            if (contactListBox.SelectedIndex != -1)
            {
                VoIPListener.SendCode("10", contacts.Find(s => contacts.IndexOf(s) == contactListBox.SelectedIndex).IpAddress);
                this.Dispatcher.Invoke(() => callSectionStackPanel.Visibility = Visibility.Visible);
            }
            else MessageBox.Show("Choose contact!");
                
        }

        private async void deleteContact(object sender, RoutedEventArgs e)
        {
            if (contactListBox.SelectedIndex == -1)
            {
                MessageBox.Show("Select contact which you want to delete!");
            }
            else
            {
                contacts.RemoveAt(contactListBox.SelectedIndex);
                await Task.Run(() => writeToJson(contacts));
                ipTextBox.Text = "";
                nicknameTextBox.Text = "";
                FillContacts();
            }
        }

        private void displayIpAddress()
        {
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                ipAddress.Foreground = Brushes.White;
                ipAddress.Text = Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ipAddress => ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
            }
            else
            {
                ipAddress.Foreground = Brushes.Red;
                ipAddress.Text = "Disconnected";
            }
        }

        private async void saveContact(object sender, RoutedEventArgs e)
        {
            if (ipTextBox.Text != String.Empty && nicknameTextBox.Text != String.Empty)
            {
                var alreadySaved = contacts.Find(c => c.IpAddress == ipTextBox.Text);
                if (alreadySaved == null)
                {
                    Contact newContact = new Contact(nicknameTextBox.Text, ipTextBox.Text);
                    contacts.Add(newContact);
                    ipTextBox.Text = "";
                    nicknameTextBox.Text = "";
                    await Task.Run(() => writeToJson(contacts));
                    FillContacts();
                }
                else
                {
                    alreadySaved.Nickname = nicknameTextBox.Text;
                    await Task.Run(() => writeToJson(contacts));
                    FillContacts();
                    ipTextBox.Text = "";
                    nicknameTextBox.Text = "";
                }
            }
            else
            {
                MessageBox.Show("Fill both TextBoxes!");
            }
        }

        private void dragWindow(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void closeWindow(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void minimizeWindow(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void writeToJson(List<Contact> contacts)
        {
            if (contacts.Count > 0)
            {
                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string fullPath = System.IO.Path.Combine(docPath, "VoLAN.txt");

                string output = JArray.FromObject(contacts).ToString();

                File.WriteAllText(fullPath, output);
            }
            else
            {
                MessageBox.Show("There is nothing to save!");
            }
        }

        private List<Contact> readFromJson()
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fullPath = System.IO.Path.Combine(docPath, "VoLAN.txt");

            if (File.Exists(fullPath))
            {
                string input = File.ReadAllText(fullPath);
                JArray parsedInput = JArray.Parse(input);
                List<Contact> result = parsedInput.ToObject<List<Contact>>();

                return result;
            }
            else
            {
                return new List<Contact>();
            }
        }

        private void FillContacts()
        {
            contactListBox.Items.Clear();
            foreach (var contact in contacts)
            {
                var tempStack = new StackPanel { Orientation = Orientation.Vertical };

                var secondStack = new StackPanel { Orientation = Orientation.Horizontal };
                var nameTxtBox = new TextBlock { Foreground = Brushes.White, Text = contact.Nickname, FontSize = 20, TextAlignment = TextAlignment.Left, Margin= new Thickness(8,0,0,0) };
                var foregroundColor = Brushes.DarkRed;
                if (contact.Status == Status.Active) foregroundColor = Brushes.Green;
                else if (contact.Status == Status.Brb) foregroundColor = Brushes.Yellow;
                var statusElipse = new Ellipse { Width = 12, Height = 12, Fill = foregroundColor, Margin = new Thickness(0, 4, 0, 0) };
                secondStack.Children.Add(statusElipse);
                secondStack.Children.Add(nameTxtBox);

                var ipTxtBox = new TextBlock { Foreground = Brushes.LightGray, Text = contact.IpAddress, FontSize = 14, TextAlignment = TextAlignment.Left };
                
                tempStack.Children.Add(secondStack);
                tempStack.Children.Add(ipTxtBox);
                contactListBox.Items.Add(tempStack);
            }
        }

        //TODO:
        //wyciszanie mikro
    }
}
