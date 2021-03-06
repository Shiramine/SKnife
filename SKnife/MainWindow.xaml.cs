﻿using Lidgren.Network;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace SKnife
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        [DllImport("kernel32")]
        static extern bool AllocConsole();
        public List<Client> Clients = new List<Client>();
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //AllocConsole();
            LoadClients();
            NServer.ClientConnected += NServer_ClientConnected;
            NServer.ClientDisconnected += NServer_ClientDisconnected;
            NServer.ClientPacketReceived += NServer_ClientPacketReceived;
            NServer.ClientConnectionApproval += NServer_ClientConnectionApproval;
            NServer.StartServer();
        }
        void SortClients()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                Clients = Clients.OrderByDescending(x => x.connected).ToList();
                for(int i = 0;i<Clients.Count;i++)
                {
                    Clients[i].Location = new Point(10, 10 + i * 210);
                }
            }));
        }
        void LoadClients()
        {
            if(!Directory.Exists("Accounts"))
                Directory.CreateDirectory("Accounts");
            DirectoryInfo di = new DirectoryInfo("Accounts");
            DirectoryInfo[] dirs = di.GetDirectories();
            foreach(DirectoryInfo dir in dirs)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
                {
                    List<string> strs = new List<string>();
                    FileStream fs = new FileStream(dir.FullName + "/info", FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(fs);
                    while (!sr.EndOfStream)
                        strs.Add(sr.ReadLine());
                    sr.Close();
                    fs.Close();
                    Clients.Add(new Client(MainGrid) { Accid = dir.Name, icon = LoadBitmap(dir.FullName + "/icon"), Action = ClientAction.Off, MoneyLimit = 0, State = (ClientState)Enum.Parse(typeof(ClientState),strs.Where(x => x.Split('=')[0] == "status").First().Split('=')[1]), connected = false  });
                }));
            }
            SortClients();
        }
        ImageSource LoadBitmap(string inputPath)
        {
            FileStream fileStream = null;
            BitmapImage img = null;
            try
            {
                fileStream = new FileStream(inputPath, FileMode.Open);
                img = new BitmapImage();
                img.BeginInit();
                img.DecodePixelWidth = 1024;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = fileStream;
                img.EndInit();
                img.Freeze();
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                    fileStream.Dispose();
                }
            }
            return img;
        }
        void NServer_ClientPacketReceived(NetConnection NC, byte MType, string Mess)
        {
            if (MType == (byte)ConnMessType.CAct)
            {
                if (Clients.Where(x => x.NC == NC).First().Action != (ClientAction)Enum.Parse(typeof(ClientAction), Mess))
                    Clients.Where(x => x.NC == NC).First().Action = (ClientAction)Enum.Parse(typeof(ClientAction), Mess);
            }
            if(MType == (byte)ConnMessType.NK)
            {
                Clients.Where(x => x.NC == NC).First().NK = true;
                Clients.Where(x => x.NC == NC).First().knives.Clear();
                string str = Crypt.Crypt.Decrypt(Mess);
                foreach (string s in str.Split(new string[] { "<::>" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Clients.Where(x => x.NC == NC).First().knives.Add(new SSKnife()
                    {
                        date = Convert.ToDateTime(s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[0]),
                        price = s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[1],
                        succ = Convert.ToBoolean(s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[2]),
                        sender = s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[3]
                    });
                }
                Clients.Where(x => x.NC == NC).First().SaveStats();
            }
        }
        void NServer_ClientDisconnected(NetConnection NC, string mess)
        {
            if (Clients.Where(x => x.NC == NC).Count() != 0)
                Clients.Where(x => x.NC == NC).First().connected = false;
        }
        void NServer_ClientConnected(NetConnection NC, string mess)
        {

        }
        ClientState NServer_ClientConnectionApproval(NetConnection NC, string Mess)
        {
            ClientState CS = ClientState.Deny;
            long ClientID = NC.RemoteUniqueIdentifier;
            string accid = Mess.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[0];
            if (!Directory.Exists("Accounts/" + accid))
                Directory.CreateDirectory("Accounts/" + accid);
            string ProfileImgLink = Mess.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[1];
            new WebClient().DownloadFile(ProfileImgLink, "Accounts/" + accid + "/icon");
            double MoneyLimit = Convert.ToDouble(Mess.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[2]);
            if (Clients.Where(x => x.Accid == accid).Count() != 0)
            {
                if (Clients.Where(x => x.Accid == accid).First().connected)
                {
                    if (Clients.Where(x => x.Accid == accid).First().NC != NC)
                    {
                        return ClientState.Deny;
                    }
                }
                Clients.Where(x => x.Accid == accid).First().NC = NC;
                Clients.Where(x => x.NC == NC).First().MoneyLimit = MoneyLimit;
                Clients.Where(x => x.NC == NC).First().Action = ClientAction.Off;
                Clients.Where(x => x.NC == NC).First().icon = LoadBitmap(AppDomain.CurrentDomain.BaseDirectory + "Accounts/" + accid + "/icon");
                Clients.Where(x => x.NC == NC).First().connected = true;

                Clients.Where(x => x.NC == NC).First().LoadStats();
                
                List<string> strs = new List<string>();
                FileStream fs = new FileStream("Accounts/" + accid + "/info", FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(fs);
                while (!sr.EndOfStream)
                    strs.Add(sr.ReadLine());
                sr.Close();
                fs.Close();
                Clients.Where(x => x.NC == NC).First().State = (ClientState)Enum.Parse(typeof(ClientState), (strs.Where(x => x.Split('=')[0] == "status").First().Split('=')[1]));
                CS = Clients.Where(x => x.NC == NC).First().State;
            }
            else
            {
                FileStream fs = new FileStream("Accounts/" + accid + "/info", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine("status=NewUser");
                sw.WriteLine("subcookies=false");
                sw.Close();
                fs.Close();
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
                {
                    Clients.Add(new Client(MainGrid) { NC = NC, Accid = accid, Action = ClientAction.Off, icon = LoadBitmap(AppDomain.CurrentDomain.BaseDirectory + "Accounts/" + accid + "/icon"), MoneyLimit = MoneyLimit, State = ClientState.NewUser, connected = true });
                }));
                CS = ClientState.NewUser;
            }
            //server.SendPacketToClient(ClientID, (byte)ConnMessType.Auth, Clients.Where(x => x.guid == ClientID).First().State.ToString());
            SortClients();
            return CS;
        }
        //void server_ClientPacketReceived(Guid ClientID, byte PacketType, string Packet)
        //{
        //    try
        //    {
        //        if(PacketType == (byte)ConnMessType.Alive)
        //        {
        //            server.SendPacketToClient(ClientID, (byte)ConnMessType.Alive, "");
        //        }
        //        if (PacketType == (byte)ConnMessType.CAct)
        //        {
        //            Clients.Where(x => x.guid == ClientID).First().Action = (ClientAction)Enum.Parse(typeof(ClientAction), Packet);
        //        }
        //        if (PacketType == (byte)ConnMessType.Auth)
        //        {
        //            string accid = Packet.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[0];
        //            if (!Directory.Exists("Accounts/" + accid))
        //                Directory.CreateDirectory("Accounts/" + accid);
        //            string ProfileImgLink = Packet.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[1];
        //            new WebClient().DownloadFile(ProfileImgLink, "Accounts/" + accid + "/icon");
        //            double MoneyLimit = Convert.ToDouble(Packet.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[2]);
        //            if (Clients.Where(x => x.Accid == accid).Count() != 0)
        //            {
        //                if (Clients.Where(x => x.Accid == accid).First().connected)
        //                {
        //                    if (Clients.Where(x => x.Accid == accid).First().guid != ClientID)
        //                    {
        //                        server.DropClient(ClientID);
        //                        return;
        //                    }
        //                }
        //                Clients.Where(x => x.Accid == accid).First().guid = ClientID;
        //                Clients.Where(x => x.guid == ClientID).First().MoneyLimit = MoneyLimit;
        //                Clients.Where(x => x.guid == ClientID).First().Action = ClientAction.Off;
        //                Clients.Where(x => x.guid == ClientID).First().icon = LoadBitmap(AppDomain.CurrentDomain.BaseDirectory + "Accounts/" + accid + "/icon");
        //                Clients.Where(x => x.guid == ClientID).First().connected = true;

        //                List<string> strs = new List<string>();
        //                FileStream fs = new FileStream("Accounts/" + accid + "/info", FileMode.Open, FileAccess.Read);
        //                StreamReader sr = new StreamReader(fs);
        //                while (!sr.EndOfStream)
        //                    strs.Add(sr.ReadLine());
        //                sr.Close();
        //                fs.Close();
        //                Clients.Where(x => x.guid == ClientID).First().State = (ClientState)Enum.Parse(typeof(ClientState), (strs.Where(x => x.Split('=')[0] == "status").First().Split('=')[1]));
        //            }
        //            else
        //            {
        //                FileStream fs = new FileStream("Accounts/" + accid + "/info", FileMode.Create, FileAccess.Write);
        //                StreamWriter sw = new StreamWriter(fs);
        //                sw.WriteLine("status=NewUser");
        //                sw.WriteLine("subcookies=false");
        //                sw.Close();
        //                fs.Close();
        //                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
        //                {
        //                    Clients.Add(new Client(MainGrid, server) { guid = ClientID, Accid = accid, Action = ClientAction.Off, icon = LoadBitmap(AppDomain.CurrentDomain.BaseDirectory + "Accounts/" + accid + "/icon"), MoneyLimit = MoneyLimit, State = ClientState.NewUser, connected = true });
        //                }));
        //            }
        //            //server.SendPacketToClient(ClientID, (byte)ConnMessType.Auth, Clients.Where(x => x.guid == ClientID).First().State.ToString());
        //            SortClients();
        //        }
        //    }
        //    catch(Exception ee)
        //    {
        //        MessageBox.Show(ee.ToString());
        //    }
        //}
        //void server_ClientDisconnected(Guid ClientID)
        //{
        //    try
        //    {
        //        if(Clients.Where(x => x.guid == ClientID).Count() != 0)
        //            Clients.Where(x => x.guid == ClientID).First().connected = false;
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show("server_ClientDisconnected error:" + e.Message + Environment.NewLine + e.ToString());
        //    }
        //}
        //void server_ClientConnected(Guid ClientID)
        //{
        //    //MainGrid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        //    //{
        //    //    try
        //    //    {
        //    //        Clients.Add(new Client(MainGrid) { guid = ClientID });
        //    //    }
        //    //    catch (Exception e)
        //    //    {
        //    //        MessageBox.Show("");
        //    //    }
        //    //}));

        //}
        public class Client
        {
            public bool NK = false;
            public List<SSKnife> knives = new List<SSKnife>();
            public Grid Interface;
            Point _Location;
            public Point Location
            {
                get { return _Location; }
                set
                {
                    _Location = value;
                    ThicknessAnimation ta = new ThicknessAnimation();
                    ta.From = Interface.Margin;
                    ta.To = new Thickness(_Location.X, _Location.Y, 0, 0);
                    ta.Duration = TimeSpan.FromMilliseconds(500);
                    ta.EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseOut };
                    Interface.BeginAnimation(MarginProperty, ta);
                }
            }
            Label L_Accid { get; set; }
            Image Img_ProfileImg { get; set; }
            TextBlock Tb_MoneyLimit { get; set; }
            TextBlock Tb_State { get; set; }
            Image Img_CAction { get; set; }
            Label L_CAction { get; set; }
            Grid opt { get; set; }
            Button B_Auth { get; set; }
            Button B_Ban { get; set; }
            Button B_Sub { get; set; }
            Button B_UnSub { get; set; }

            bool _connected;
            public bool connected
            {
                get { return _connected; }
                set
                {
                    _connected = value;
                    Interface.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        if (!_connected)
                        {
                            Action = ClientAction.Off;
                            if (_State == ClientState.Auth)
                            {
                                Tb_State.Text = "Offline";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                            if (_State == ClientState.Banned)
                            {
                                Tb_State.Text = "Banned";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 0, 0));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                            if (_State == ClientState.NewUser)
                            {
                                Tb_State.Text = "NewUser";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 0));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                        }
                        else
                        {
                            if (_State == ClientState.Auth)
                            {
                                Tb_State.Text = "Auth";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 200, 0));
                                Interface.Opacity = 1;
                                L_Accid.Opacity = 1;
                                Img_ProfileImg.Opacity = 1;
                                Tb_MoneyLimit.Opacity = 1;
                                Tb_State.Opacity = 1;
                                Img_CAction.Opacity = 1;
                                L_CAction.Opacity = 1;
                            }
                            if (_State == ClientState.Banned)
                            {
                                Tb_State.Text = "Banned";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 0, 0));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                            if (_State == ClientState.NewUser)
                            {
                                Tb_State.Text = "NewUser";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 0));
                                Interface.Opacity = 1;
                                L_Accid.Opacity = 1;
                                Img_ProfileImg.Opacity = 1;
                                Tb_MoneyLimit.Opacity = 1;
                                Tb_State.Opacity = 1;
                                Img_CAction.Opacity = 1;
                                L_CAction.Opacity = 1;
                            }
                        }
                    }));
                }
            }

            bool _showOpt;
            bool showOpt
            {
                get { return _showOpt; }
                set
                {
                    _showOpt = value;
                    if(_showOpt)
                    {
                        ThicknessAnimation ta = new ThicknessAnimation();
                        ta.From = Img_ProfileImg.Margin;
                        ta.To = new Thickness(-150, Img_ProfileImg.Margin.Top, 0, 0);
                        ta.Duration = TimeSpan.FromMilliseconds(250);
                        ta.EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseOut };
                        Img_ProfileImg.BeginAnimation(MarginProperty, ta);

                        ThicknessAnimation ta1 = new ThicknessAnimation();
                        ta1.From = opt.Margin;
                        ta1.To = new Thickness(opt.Margin.Left, 50, 0, 0);
                        ta1.Duration = TimeSpan.FromMilliseconds(250);
                        ta1.EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseOut };
                        opt.BeginAnimation(MarginProperty, ta1);
                    }
                    else
                    {
                        ThicknessAnimation ta = new ThicknessAnimation();
                        ta.From = Img_ProfileImg.Margin;
                        ta.To = new Thickness(10, Img_ProfileImg.Margin.Top, 0, 0);
                        ta.Duration = TimeSpan.FromMilliseconds(250);
                        ta.EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseOut };
                        Img_ProfileImg.BeginAnimation(MarginProperty, ta);

                        ThicknessAnimation ta1 = new ThicknessAnimation();
                        ta1.From = opt.Margin;
                        ta1.To = new Thickness(opt.Margin.Left, 210, 0, 0);
                        ta1.Duration = TimeSpan.FromMilliseconds(250);
                        ta1.EasingFunction = new PowerEase() { EasingMode = EasingMode.EaseOut };
                        opt.BeginAnimation(MarginProperty, ta1);
                    }
                }
            }

            bool _SubCookies { get; set; }
            public NetConnection NC { get; set; }

            string _Accid;
            public string Accid
            {
                get { return _Accid; }
                set
                {
                    _Accid = value; 
                    L_Accid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        L_Accid.Content = _Accid;
                    }));
                }
            }

            ImageSource _icon;
            public ImageSource icon
            {
                get { return _icon; }
                set
                {
                    _icon = value;
                    Img_ProfileImg.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
                    {
                        Img_ProfileImg.Source = _icon;
                    }));
                }
            }

            double _MoneyLimit;
            public double MoneyLimit
            {
                get { return _MoneyLimit; }
                set
                {
                    _MoneyLimit = value;
                    Tb_MoneyLimit.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        Tb_MoneyLimit.Text = "Money limit: " + _MoneyLimit.ToString();
                    }));
                }
            }

            ClientState _State;
            public ClientState State
            {
                get { return _State; }
                set
                {
                    _State = value;
                    Interface.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        List<string> strs = new List<string>();
                        FileStream fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "Accounts/" + Accid + "/info", FileMode.Open, FileAccess.Read);
                        StreamReader sr = new StreamReader(fs);
                        while (!sr.EndOfStream)
                            strs.Add(sr.ReadLine());
                        sr.Close();
                        fs.Close();
                        
                        fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "Accounts/" + Accid + "/info", FileMode.Truncate, FileAccess.Write);
                        StreamWriter sw = new StreamWriter(fs);
                        foreach(string s in strs)
                        {
                            if (s.Split('=')[0] == "status")
                                sw.WriteLine("status=" + State.ToString());
                            else
                                sw.WriteLine(s);
                        }
                        sw.Close();
                        fs.Close();
                        if (!_connected)
                        {
                            if (_State == ClientState.Auth)
                            {
                                Tb_State.Text = "Offline";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                            if (_State == ClientState.Banned)
                            {
                                Tb_State.Text = "Banned";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 0, 0));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                            if (_State == ClientState.NewUser)
                            {
                                Tb_State.Text = "NewUser";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 0));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                            }
                        }
                        else
                        {
                            if (_State == ClientState.Auth)
                            {
                                Tb_State.Text = "Auth";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 200, 0));
                                Interface.Opacity = 1;
                                L_Accid.Opacity = 1;
                                Img_ProfileImg.Opacity = 1;
                                Tb_MoneyLimit.Opacity = 1;
                                Tb_State.Opacity = 1;
                                Img_CAction.Opacity = 1;
                                L_CAction.Opacity = 1;
                                NServer.Send(NC, (byte)ConnMessType.Auth, "Auth");
                                    
                            }
                            if (_State == ClientState.Banned)
                            {
                                Tb_State.Text = "Banned";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 0, 0));
                                Interface.Opacity = 0.5;
                                L_Accid.Opacity = 0.5;
                                Img_ProfileImg.Opacity = 0.5;
                                Tb_MoneyLimit.Opacity = 0.5;
                                Tb_State.Opacity = 0.5;
                                Img_CAction.Opacity = 0.5;
                                L_CAction.Opacity = 0.5;
                                NServer.Send(NC, (byte)ConnMessType.Auth, "Banned");
                            }
                            if (_State == ClientState.NewUser)
                            {
                                Tb_State.Text = "NewUser";
                                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 0));
                                Interface.Opacity = 1;
                                L_Accid.Opacity = 1;
                                Img_ProfileImg.Opacity = 1;
                                Tb_MoneyLimit.Opacity = 1;
                                Tb_State.Opacity = 1;
                                Img_CAction.Opacity = 1;
                                L_CAction.Opacity = 1;
                                NServer.Send(NC, (byte)ConnMessType.Auth, "NewUser");
                            }
                        }
                    }));
                }
            }

            ClientAction _Action;
            public ClientAction Action
            {
                get { return _Action; }
                set
                {
                    _Action = value;
                    if (_Action == ClientAction.Buying)
                    {
                        Img_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "loading.gif");
                            image.EndInit();
                            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(Img_CAction, image);
                        }));
                        L_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            L_CAction.Content = "Buying";
                        }));
                        Interface.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            ColorAnimation ca = new ColorAnimation();
                            ca.From = ((SolidColorBrush)Interface.Background).Color;
                            ca.To = Color.FromArgb(255, 127, 0, 0);
                            ca.Duration = TimeSpan.FromMilliseconds(500);
                            ca.EasingFunction = new PowerEase()
                            {
                                EasingMode = EasingMode.EaseOut
                            };
                            Interface.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
                        }));
                    } 
                    if (_Action == ClientAction.GetKnife)
                    {
                        Img_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "loading.gif");
                            image.EndInit();
                            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(Img_CAction, image);
                        }));
                        L_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            L_CAction.Content = "GettingPrice";
                        }));
                        Interface.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            ColorAnimation ca = new ColorAnimation();
                            ca.From = ((SolidColorBrush)Interface.Background).Color;
                            ca.To = Color.FromArgb(255, 127, 127, 0);
                            ca.Duration = TimeSpan.FromMilliseconds(500);
                            ca.EasingFunction = new PowerEase()
                            {
                                EasingMode = EasingMode.EaseOut
                            };
                            Interface.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
                        }));
                    } 
                    if (_Action == ClientAction.Off)
                    {
                        Img_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "error.png");
                            image.EndInit();
                            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(Img_CAction, image);
                        }));
                        L_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            L_CAction.Content = "Off";
                        }));
                        Interface.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            ColorAnimation ca = new ColorAnimation();
                            ca.From = ((SolidColorBrush)Interface.Background).Color;
                            ca.To = Color.FromArgb(255, 16, 16, 16);
                            ca.Duration = TimeSpan.FromMilliseconds(500);
                            ca.EasingFunction = new PowerEase()
                            {
                                EasingMode = EasingMode.EaseOut
                            };
                            Interface.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
                        }));
                    } 
                    if (_Action == ClientAction.Search)
                    {
                        Img_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "sloading.gif");
                            image.EndInit();
                            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(Img_CAction, image);
                        }));

                        L_CAction.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            L_CAction.Content = "Search";
                        }));
                        Interface.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            ColorAnimation ca = new ColorAnimation();
                            ca.From = ((SolidColorBrush)Interface.Background).Color;
                            if(!NK)
                                ca.To = Color.FromArgb(255, 16, 16, 16);
                            else
                                ca.To = Color.FromArgb(127, 16, 255, 16);
                            ca.Duration = TimeSpan.FromMilliseconds(500);
                            ca.EasingFunction = new PowerEase()
                            {
                                EasingMode = EasingMode.EaseOut
                            };
                            Interface.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
                        }));
                    }
                }
            }

            //public void SubCookies(Nading.Network.Server.clsServer server, bool sub)
            //{
            //    _SubCookies = sub;
            //}
            public Client(Grid parent)
            {

                Interface = new Grid();
                Interface.HorizontalAlignment = HorizontalAlignment.Left;
                Interface.VerticalAlignment = VerticalAlignment.Top;
                Interface.Width = 500;
                Interface.Height = 200;
                Interface.Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));
                Interface.Margin = new Thickness(10, 10, 0, 0);
                Interface.ClipToBounds = true;
                Interface.MouseLeave += Interface_MouseLeave;
                parent.Children.Add(Interface);

                Location = new Point(10, 10);

                L_Accid = new Label();
                L_Accid.HorizontalAlignment = HorizontalAlignment.Left;
                L_Accid.VerticalAlignment = VerticalAlignment.Top;
                L_Accid.FontSize = 20;
                L_Accid.Width = 140;
                L_Accid.Margin = new Thickness(10, 10, 0, 0);
                Interface.Children.Add(L_Accid);

                Img_ProfileImg = new Image();
                RenderOptions.SetBitmapScalingMode(Img_ProfileImg, BitmapScalingMode.HighQuality);
                Img_ProfileImg.HorizontalAlignment = HorizontalAlignment.Left;
                Img_ProfileImg.VerticalAlignment = VerticalAlignment.Top;
                Img_ProfileImg.Margin = new Thickness(10, 50, 0, 0);
                Img_ProfileImg.Width = 140;
                Img_ProfileImg.Height = 140;
                Img_ProfileImg.PreviewMouseRightButtonUp += Img_ProfileImg_PreviewMouseRightButtonUp;
                Img_ProfileImg.PreviewMouseLeftButtonUp += Interface_PreviewMouseLeftButtonUp;
                Interface.Children.Add(Img_ProfileImg);

                Tb_MoneyLimit = new TextBlock();
                Tb_MoneyLimit.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                Tb_MoneyLimit.VerticalAlignment = VerticalAlignment.Top;
                Tb_MoneyLimit.HorizontalAlignment = HorizontalAlignment.Left;
                Tb_MoneyLimit.Width = 190;
                Tb_MoneyLimit.Height = 30;
                Tb_MoneyLimit.Margin = new Thickness(155, 50, 0, 0);
                Tb_MoneyLimit.TextWrapping = TextWrapping.Wrap;
                Tb_MoneyLimit.Padding = new Thickness(3);
                Tb_MoneyLimit.FontSize = 18;
                Interface.Children.Add(Tb_MoneyLimit);

                Tb_State = new TextBlock();
                Tb_State.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                Tb_State.VerticalAlignment = VerticalAlignment.Top;
                Tb_State.HorizontalAlignment = HorizontalAlignment.Left;
                Tb_State.Width = 190;
                Tb_State.Height = 50;
                Tb_State.Margin = new Thickness(155, 85, 0, 0);
                Tb_State.TextWrapping = TextWrapping.Wrap;
                Tb_State.Padding = new Thickness(3);
                Tb_State.FontSize = 18;
                Interface.Children.Add(Tb_State);

                Label L = new Label();
                L.HorizontalAlignment = HorizontalAlignment.Left;
                L.VerticalAlignment = VerticalAlignment.Top;
                L.FontSize = 20;
                L.Width = 140;
                L.Content = "Current action:";
                L.Margin = new Thickness(155, 153, 0, 0);
                Interface.Children.Add(L);

                Img_CAction = new Image();
                RenderOptions.SetBitmapScalingMode(Img_CAction, BitmapScalingMode.HighQuality);
                Img_CAction.HorizontalAlignment = HorizontalAlignment.Left;
                Img_CAction.VerticalAlignment = VerticalAlignment.Top;
                Img_CAction.Margin = new Thickness(350, 10, 0, 0);
                Img_CAction.Width = 140;
                Img_CAction.Height = 140;
                Interface.Children.Add(Img_CAction);

                L_CAction = new Label();
                L_CAction.HorizontalAlignment = HorizontalAlignment.Left;
                L_CAction.VerticalAlignment = VerticalAlignment.Top;
                L_CAction.FontSize = 20;
                L_CAction.Width = 140;
                L_CAction.Margin = new Thickness(350, 155, 0, 0);
                Interface.Children.Add(L_CAction);

                opt = new Grid();
                opt.HorizontalAlignment = HorizontalAlignment.Left;
                opt.VerticalAlignment = VerticalAlignment.Top;
                opt.Width = 140;
                opt.Height = 140;
                opt.Margin = new Thickness(10, 210, 0, 0);
                Interface.Children.Add(opt);

                B_Auth = new Button();
                B_Auth.Style = Application.Current.FindResource("SquareButtonStyle") as Style;
                B_Auth.HorizontalAlignment = HorizontalAlignment.Left;
                B_Auth.VerticalAlignment = VerticalAlignment.Top;
                B_Auth.Width = 140;
                B_Auth.Height = 35;
                B_Auth.Content = "Auth";
                B_Auth.Margin = new Thickness(0, 0, 0, 0);
                B_Auth.Click += B_Auth_Click;
                opt.Children.Add(B_Auth);

                B_Ban = new Button();
                B_Ban.Style = Application.Current.FindResource("SquareButtonStyle") as Style;
                B_Ban.HorizontalAlignment = HorizontalAlignment.Left;
                B_Ban.VerticalAlignment = VerticalAlignment.Top;
                B_Ban.Width = 140;
                B_Ban.Height = 35;
                B_Ban.Content = "Ban";
                B_Ban.Margin = new Thickness(0, 35, 0, 0);
                B_Ban.Click += B_Ban_Click;
                opt.Children.Add(B_Ban);

                B_Sub = new Button();
                B_Sub.Style = Application.Current.FindResource("SquareButtonStyle") as Style;
                B_Sub.HorizontalAlignment = HorizontalAlignment.Left;
                B_Sub.VerticalAlignment = VerticalAlignment.Top;
                B_Sub.Width = 140;
                B_Sub.Height = 35;
                B_Sub.Content = "Sub";
                B_Sub.Margin = new Thickness(0, 70, 0, 0);
                B_Sub.Click += B_Sub_Click;
                opt.Children.Add(B_Sub);

                B_UnSub = new Button();
                B_UnSub.Style = Application.Current.FindResource("SquareButtonStyle") as Style;
                B_UnSub.HorizontalAlignment = HorizontalAlignment.Left;
                B_UnSub.VerticalAlignment = VerticalAlignment.Top;
                B_UnSub.Width = 140;
                B_UnSub.Height = 35;
                B_UnSub.Content = "Unsub";
                B_UnSub.Margin = new Thickness(0, 105, 0, 0);
                B_UnSub.Click += B_UnSub_Click;
                opt.Children.Add(B_UnSub);
                
            }

            void Interface_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
            {
                if (Action == ClientAction.Search || Action == ClientAction.Off)
                {
                    Interface.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                    {
                        ColorAnimation ca = new ColorAnimation();
                        ca.From = ((SolidColorBrush)Interface.Background).Color;
                        ca.To = Color.FromArgb(255, 16, 16, 16);
                        ca.Duration = TimeSpan.FromMilliseconds(500);
                        ca.EasingFunction = new PowerEase()
                        {
                            EasingMode = EasingMode.EaseOut
                        };
                        Interface.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
                    }));
                }
                Stats s = new Stats(knives);
                s.Show();
                NK = false;
            }

            void Interface_MouseLeave(object sender, MouseEventArgs e)
            {
                if (showOpt)
                    showOpt = false;
            }

            void Img_ProfileImg_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
            {
                if (!showOpt)
                    showOpt = true;
            }
            void B_Auth_Click(object sender, RoutedEventArgs e)
            {
                if(State == ClientState.Banned || State == ClientState.NewUser)
                {
                    State = ClientState.Auth;
                }
            }
            void B_Ban_Click(object sender, RoutedEventArgs e)
            {
                State = ClientState.Banned;
            }
            void B_Sub_Click(object sender, RoutedEventArgs e)
            {

            }
            void B_UnSub_Click(object sender, RoutedEventArgs e)
            {

            }

            public void LoadStats()
            {
                try
                {
                    knives.Clear();
                    FileStream fs = new FileStream("Accounts/" + Accid + "/Knives.key", FileMode.Open, FileAccess.Read);
                    StreamReader sr = new StreamReader(fs);
                    string str = Crypt.Crypt.Decrypt(sr.ReadToEnd());
                    sr.Close();
                    fs.Close();
                    if (str != "nothing")
                    {
                        foreach (string s in str.Split(new string[] { "<::>" }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            knives.Add(new SSKnife()
                            {
                                date = Convert.ToDateTime(s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[0]),
                                price = s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[1],
                                succ = Convert.ToBoolean(s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[2]),
                                sender = s.Split(new string[] { "<:>" }, StringSplitOptions.RemoveEmptyEntries)[3]
                            });
                        }
                    }
                    
                }
                catch
                {
                    
                }
            }
            public void SaveStats()
            {
                FileStream fs = new FileStream("Accounts/" + Accid + "/Knives.key", FileMode.Create, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                string str = "";
                foreach (SSKnife k in knives)
                    str += k.date.ToString() + "<:>" + k.price + "<:>" + k.succ.ToString() + "<:>" + k.sender + "<::>";
                string estr = Crypt.Crypt.Encrypt(str);
                sw.Write(estr);
                sw.Close();
                fs.Close();
            }
        }
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            NServer.Shutdown();
            //server.StopServer();
        }
        private void MainGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            foreach (Client c in Clients)
            {
                c.Location = new Point(c.Location.X, c.Location.Y + e.Delta);
            }
        }
    }
    public enum ConnMessType
    {
        Auth,
        Sub,
        Log,
        CAct,
        Alive,
        NK
    }
    public enum ClientAction
    {
        Off,
        Search,
        GetKnife,
        Buying,
    }
    public enum ClientState
    {
        NewUser,
        Auth,
        Banned,
        Deny
    }
    public static class NServer
    {
        public static event ClientConnectedEventHandler ClientConnected;
        public delegate void ClientConnectedEventHandler(NetConnection NC, string mess);

        public static event ClientDisconnectedEventHandler ClientDisconnected;
        public delegate void ClientDisconnectedEventHandler(NetConnection NC, string mess);

        public static event ClientPacketReceivedEventHandler ClientPacketReceived;
        public delegate void ClientPacketReceivedEventHandler(NetConnection NC, byte MType, string Mess);

        public static event ClientConnectionApprovalEventHandler ClientConnectionApproval;
        public delegate ClientState ClientConnectionApprovalEventHandler(NetConnection NC, string Mess);

        public static NetServer s_server;
        static NServer()
        {
            NetPeerConfiguration config = new NetPeerConfiguration("Knife");
            config.MaximumConnections = 100;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.Port = 18346;
            config.ConnectionTimeout = 10;
            s_server = new NetServer(config);
            s_server.RegisterReceivedCallback(new SendOrPostCallback(callb));
        }
        public static void StartServer()
        {
            s_server.Start();
        }
        public static void Shutdown()
        {
            s_server.Shutdown("Requested by user");
        }
        public static void Send(NetConnection NC, byte MType, string Mess)
        {
            string ToSend = MType.ToString() + "<:SS:>" + Mess;
            NetOutgoingMessage om = s_server.CreateMessage();
            om.Write(ToSend);
            s_server.SendMessage(om, NC, NetDeliveryMethod.ReliableOrdered, 0);
        }
        public static void Send(long NC, byte MType, string Mess)
        {
            string ToSend = MType.ToString() + "<:SS:>" + Mess;
            NetOutgoingMessage om = s_server.CreateMessage();
            om.Write(ToSend);
            s_server.SendMessage(om, s_server.Connections.Where(x => x.RemoteUniqueIdentifier == NC).First(), NetDeliveryMethod.ReliableOrdered, 0);
        }
        public static void Send(List<NetConnection> NC, byte MType, string Mess)
        {
            string ToSend = MType.ToString() + "<:SS:>" + Mess;
            NetOutgoingMessage om = s_server.CreateMessage();
            om.Write(ToSend);
            s_server.SendMessage(om, NC, NetDeliveryMethod.ReliableOrdered, 0);
        }
        static void callb(object obj)
        {
            NetIncomingMessage im;
            while ((im = s_server.ReadMessage()) != null)
            {
                // handle incoming message
                switch (im.MessageType)
                {
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.ErrorMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        string text = im.ReadString();
                        //Output(text);
                        break;

                    case NetIncomingMessageType.StatusChanged:
                        NetConnectionStatus status = (NetConnectionStatus)im.ReadByte();
                        if (status == NetConnectionStatus.Connected)
                        {
                            ClientConnectedEventHandler CE = ClientConnected;
                            if (CE != null)
                                CE(im.SenderConnection, im.SenderConnection.RemoteHailMessage.ReadString());
                        }
                        if (status == NetConnectionStatus.Disconnected)
                        {
                            ClientDisconnectedEventHandler DE = ClientDisconnected;
                            if (DE != null)
                                DE(im.SenderConnection, im.ReadString());
                        }
                        if (status == NetConnectionStatus.RespondedAwaitingApproval)
                        {
                            ClientConnectionApprovalEventHandler CA = ClientConnectionApproval;
                            if(CA != null)
                                im.SenderConnection.Approve(s_server.CreateMessage(CA(im.SenderConnection, im.SenderConnection.RemoteHailMessage.ReadString()).ToString()));
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        string msg = im.ReadString();
                        byte MType = Convert.ToByte(msg.Split(new string[] { "<:SS:>" }, StringSplitOptions.None)[0]);
                        string Mess = msg.Split(new string[] { "<:SS:>" }, StringSplitOptions.None)[1];
                        ClientPacketReceivedEventHandler RE = ClientPacketReceived;
                        if (RE != null)
                        {
                            RE(im.SenderConnection, MType, Mess);
                        }
                        break;
                    default:
                        //Output("Unhandled type: " + im.MessageType + " " + im.LengthBytes + " bytes " + im.DeliveryMethod + "|" + im.SequenceChannel);
                        break;
                }
                s_server.Recycle(im);
            }
        }
    }
    public class SSKnife
    {
        public DateTime date { get; set; }
        public string price { get; set; }
        public bool succ { get; set; }
        public string sender { get; set; }
    }
}

//Auth
//Ban
//SubCookie
//
//
//
//
//
//
//
//
//
//
//
//
//