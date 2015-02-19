﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using Microsoft.Win32;

using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using SharpTox.Core;
using SharpTox.Av;

using Toxy.Views;
using Toxy.Common;
using Toxy.ToxHelpers;
using Toxy.ViewModels;
using Toxy.Extenstions;
using Toxy.Common.Transfers;

using Path = System.IO.Path;
using Brushes = System.Windows.Media.Brushes;

using SQLite;
using NAudio.Wave;
using SharpTox.Vpx;

namespace Toxy
{
    public partial class MainWindow : MetroWindow
    {
        private Tox tox;
        private ToxAv toxav;
        private ToxCall call;

        private Dictionary<int, FlowDocument> convdic = new Dictionary<int, FlowDocument>();
        private Dictionary<int, FlowDocument> groupdic = new Dictionary<int, FlowDocument>();
        private List<FileTransfer> transfers = new List<FileTransfer>();

        private bool resizing;
        private bool focusTextbox;
        private bool typing;
        private bool savingSettings;
        private bool forceClose;

        private Accent oldAccent;
        private AppTheme oldAppTheme;

        private Config config;
        private AvatarStore avatarStore;

        System.Windows.Forms.NotifyIcon nIcon = new System.Windows.Forms.NotifyIcon();

        private Icon notifyIcon;
        private Icon newMessageNotifyIcon;

        private SQLiteAsyncConnection dbConnection;

        private string toxDataDir
        {
            get
            {
                if (!config.Portable)
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tox");
                else
                    return Environment.CurrentDirectory;
            }
        }

        private string toxDataFilename
        {
            get
            {
                return Path.Combine(toxDataDir, string.Format("{0}.tox", string.IsNullOrEmpty(config.ProfileName) ? tox.Id.PublicKey.GetString().Substring(0, 10) : config.ProfileName));
            }
        }

        private string toxOldDataFilename
        {
            get
            {
                return Path.Combine(toxDataDir, "tox_save");
            }
        }

        private string configFilename = "config.xml";

        private string dbFilename
        {
            get
            {
                return Path.Combine(toxDataDir, "toxy.db");
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();
            Debug.AutoFlush = true;

            if (File.Exists(configFilename))
            {
                config = ConfigTools.Load(configFilename);
            }
            else
            {
                config = new Config();
                ConfigTools.Save(config, configFilename);
            }

            avatarStore = new AvatarStore(toxDataDir);
            applyConfig();
        }

        #region Tox EventHandlers
        private void tox_OnGroupTitleChanged(object sender, ToxEventArgs.GroupTopicEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group == null)
                    return;

                group.Name = e.Topic;

                if (e.PeerNumber != -1)
                    group.AdditionalInfo = string.Format("Topic set by: {0}", tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber));
            })));
        }

        private void tox_OnGroupNamelistChange(object sender, ToxEventArgs.GroupPeerlistUpdateEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);

                if (group != null)
                    RearrangeGroupPeerList(group);

                /*if (e.Change == ToxChatChange.PeerAdd || e.Change == ToxChatChange.PeerDel)
                    group.StatusMessage = string.Format("Peers online: {0}", tox.GetGroupMemberCount(group.ChatNumber));

                switch (e.Change)
=======
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group != null)
>>>>>>> origin/master
                {
                    if (e.Change == ToxChatChange.PeerAdd || e.Change == ToxChatChange.PeerDel)
                        group.StatusMessage = string.Format("Peers online: {0}", tox.GetGroupMemberCount(group.ChatNumber));

                    switch (e.Change)
                    {
                        case ToxChatChange.PeerAdd:
                            {
                                RearrangeGroupPeerList(group);
                                break;
                            }
<<<<<<< HEAD

                            break;
                        }
                }*/
            })));
        }

        private void tox_OnGroupAction(object sender, ToxEventArgs.GroupActionEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group == null)
                    return;

                var peer = group.PeerList.GetPeerByPeerNumber(e.PeerNumber);
                if (peer != null && peer.Ignored)
                    return;

                    MessageData data = new MessageData() { Username = "*  ", Message = string.Format("{0} {1}", tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber), e.Action), IsAction = true, Timestamp = DateTime.Now, IsGroupMsg = true, IsSelf = false };
                    AddActionToView(e.GroupNumber, data, true);

                    MessageAlertIncrement(group);

                    if (group.Selected)
                        ScrollChatBox();

                    if (ViewModel.MainToxyUser.ToxStatus != ToxUserStatus.Busy)
                        this.Flash();
            })));
        }

        private void tox_OnGroupMessage(object sender, ToxEventArgs.GroupMessageEventArgs e)
        {
            MessageData data = new MessageData() { Username = tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber), Message = e.Message, Timestamp = DateTime.Now, IsGroupMsg = true, IsSelf = false };

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group == null)
                    return;

                var peer = group.PeerList.GetPeerByPeerNumber(e.PeerNumber);
                if (peer != null && peer.Ignored)
                    return;

                AddMessageToView(e.GroupNumber, data, true);
                MessageAlertIncrement(group);

                if (group.Selected)
                    ScrollChatBox();

                if (ViewModel.MainToxyUser.ToxStatus != ToxUserStatus.Busy)
                    this.Flash();

                nIcon.Icon = newMessageNotifyIcon;
                ViewModel.HasNewMessage = true;
            })));
        }

        /*private async void tox_OnGroupInvite(object sender, ToxEventArgs.GroupInviteEventArgs e)
        {
            int number;

            if (e.GroupType == ToxGroupType.Text)
            {
                number = tox.JoinGroup(e.Data);
            }
            else if (e.GroupType == ToxGroupType.Av)
            {
                if (call != null)
                {
                    await Dispatcher.BeginInvoke(((Action)(() =>
                    {
                        this.ShowMessageAsync("Error", "Could not join audio groupchat, there's already a call in progress.");
                    })));
                    return;
                }
                else
                {
                    number = toxav.JoinAvGroupchat(e.FriendNumber, e.Data);
                    call = new ToxGroupCall(toxav, number);
                    call.FilterAudio = config.FilterAudio;
                    call.Start(config.InputDevice, config.OutputDevice, ToxAv.DefaultCodecSettings);
                }
            }
            else
            {
                return;
            }

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(number);

                if (group != null)
                    SelectGroupControl(group);
                else if (number != -1)
                    AddGroupToView(number, e.GroupType);
            })));
        }*/

        private void tox_OnAvatarInfo(object sender, ToxEventArgs.AvatarInfoEventArgs e)
        {
            Debug.WriteLine(string.Format("Received avatar info from {0}", e.FriendNumber));

            var friend = Dispatcher.Invoke(() => ViewModel.GetFriendObjectByNumber(e.FriendNumber));
            if (friend == null)
                return;

            if (e.Format == ToxAvatarFormat.None)
            {
                Debug.WriteLine(string.Format("Received ToxAvatarFormat.None ({0})", e.FriendNumber));
                avatarStore.Delete(tox.GetClientId(e.FriendNumber));

                Dispatcher.BeginInvoke(((Action)(() =>
                {
                    friend.AvatarBytes = null;
                    friend.Avatar = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/profilepicture.png"));
                })));
            }
            else
            {
                Debug.WriteLine(string.Format("Received ToxAvatarFormat.Png ({0})", e.FriendNumber));

                if (friend.AvatarBytes == null || friend.AvatarBytes.Length == 0)
                {
                    if (!avatarStore.Contains(tox.GetClientId(e.FriendNumber)))
                    {
                        Debug.WriteLine(string.Format("Avatar ({0}) does not exist on disk, requesting data", e.FriendNumber));

                        if (!tox.RequestAvatarData(e.FriendNumber))
                            Debug.WriteLine(string.Format("Could not request avatar data from {0}: {1}", e.FriendNumber, getFriendName(e.FriendNumber)));
                    }
                }
                else
                {
                    Debug.WriteLine(string.Format("Comparing given hash to the avatar we already have ({0})", e.FriendNumber));

                    if (tox.GetHash(friend.AvatarBytes).SequenceEqual(e.Hash))
                    {
                        Debug.WriteLine(string.Format("We already have this avatar, ignore ({0})", e.FriendNumber));
                        return;
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("Hashes don't match, requesting avatar data... ({0})", e.FriendNumber));

                        if (!tox.RequestAvatarData(e.FriendNumber))
                            Debug.WriteLine(string.Format("Could not request avatar date from {0}: {1}", e.FriendNumber, getFriendName(e.FriendNumber)));
                    }
                }
            }
        }

        private void tox_OnAvatarData(object sender, ToxEventArgs.AvatarDataEventArgs e)
        {
            Debug.WriteLine(string.Format("Received avatar data from {0}", e.FriendNumber));

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend == null)
                    return;

                if (friend.AvatarBytes != null && tox.GetHash(friend.AvatarBytes).SequenceEqual(e.Avatar.Hash))
                {
                    Debug.WriteLine("Received avatar data unexpectedly, ignoring");
                }
                else
                {
                    try
                    {
                        friend.AvatarBytes = e.Avatar.Data;

                        Debug.WriteLine(string.Format("Starting task to apply the new avatar ({0})", e.FriendNumber));
                        applyAvatar(friend, e.Avatar.Data);
                    }
                    catch
                    {
                        Debug.WriteLine(string.Format("Received invalid avatar data ({0})", e.FriendNumber));
                    }
                }
            })));
        }

        private void tox_OnDisconnected(object sender, ToxEventArgs.ConnectionEventArgs e)
        {
            SetStatus(ToxUserStatus.Invalid, false);
        }

        private void tox_OnConnected(object sender, ToxEventArgs.ConnectionEventArgs e)
        {
            SetStatus(tox.Status, false);
        }

        private void tox_OnReadReceipt(object sender, ToxEventArgs.ReadReceiptEventArgs e)
        {
            //a flowdocument should already be created, but hey, just in case
            if (!convdic.ContainsKey(e.FriendNumber))
                return;

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                Paragraph para = (Paragraph)convdic[e.FriendNumber].FindChildren<TableRow>().Where(r => !(r.Tag is FileTransfer) && ((MessageData)(r.Tag)).Id == e.Receipt).First().FindChildren<TableCell>().ToArray()[1].Blocks.FirstBlock;

                if (para == null)
                    return; //row or cell doesn't exist? odd, just return

                if (config.Theme == "BaseDark")
                    para.Foreground = Brushes.White;
                else
                    para.Foreground = Brushes.Black;
            })));
        }

        private void tox_OnFileControl(object sender, ToxEventArgs.FileControlEventArgs e)
        {
            switch (e.Control)
            {
                case ToxFileControl.Finished:
                    {
                        var transfer = GetFileTransfer(e.FriendNumber, e.FileNumber);
                        if (transfer == null)
                            break;

                        transfer.Kill(true);
                        transfers.Remove(transfer);

                        if (transfer.GetType() != typeof(FileSender))
                            tox.FileSendControl(transfer.FriendNumber, 1, transfer.FileNumber, ToxFileControl.Finished, new byte[0]);

                        break;
                    }

                case ToxFileControl.Accept:
                    {
                        var transfer = GetFileTransfer(e.FriendNumber, e.FileNumber);
                        if (transfer == null)
                            break;

                        if (!transfer.Paused)
                        {
                            if (transfer.GetType() == typeof(FileSender))
                            {
                                FileSender ft = (FileSender)transfer;
                                ft.Tag.StartTransfer();
                                ft.Start();
                            }
                            else if (transfer.Broken && transfer.GetType() == typeof(FileReceiver))
                            {
                                transfer.Broken = false;
                                Debug.WriteLine(string.Format("Received {0}, resuming broken file transfer", e.Control));
                            }
                        }
                        else
                        {
                            transfer.Paused = false;
                        }

                        break;
                    }

                case ToxFileControl.Kill:
                    {
                        var transfer = GetFileTransfer(e.FriendNumber, e.FileNumber);
                        if (transfer == null)
                            break;

                        transfer.Kill(false);
                        transfers.Remove(transfer);
                        break;
                    }

                case ToxFileControl.ResumeBroken:
                    {
                        var transfer = GetFileTransfer(e.FriendNumber, e.FileNumber) as FileSender;
                        if (transfer == null || e.Data.Length != sizeof(long))
                            break;

                        long index = (long)BitConverter.ToUInt64(e.Data, 0);

                        transfer.RewindStream(index);
                        transfer.Broken = false;
                        tox.FileSendControl(e.FriendNumber, 0, transfer.FileNumber, ToxFileControl.Accept, new byte[0]);

                        Debug.WriteLine(string.Format("Received {0}, resuming at index: {1}", e.Control, index));
                        break;
                    }
                case ToxFileControl.Pause:
                    {
                        var transfer = GetFileTransfer(e.FriendNumber, e.FileNumber) as FileTransfer;
                        if (transfer == null)
                            break;

                        transfer.Paused = true;
                        break;
                    }
            }

            Debug.WriteLine(string.Format("Received file control: {0} from {1}", e.Control, getFriendName(e.FriendNumber)));
        }

        private void tox_OnFileData(object sender, ToxEventArgs.FileDataEventArgs e)
        {
            var transfer = GetFileTransfer(e.FriendNumber, e.FileNumber) as FileReceiver;
            if (transfer == null)
            {
                Debug.WriteLine("Hoooold your horses, we don't know about this file transfer!");
                return;
            }

            if (transfer.Broken || transfer.Paused)
                return;

            transfer.ProcessReceivedData(e.Data);
        }

        private void tox_OnFileSendRequest(object sender, ToxEventArgs.FileSendRequestEventArgs e)
        {
            if (!convdic.ContainsKey(e.FriendNumber))
                convdic.Add(e.FriendNumber, FlowDocumentExtensions.CreateNewDocument());

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var transfer = new FileReceiver(tox, e.FileNumber, e.FriendNumber, (long)e.FileSize, e.FileName, e.FileName);
                var control = convdic[e.FriendNumber].AddNewFileTransfer(tox, transfer);
                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                transfer.Tag = control;

                if (friend != null)
                {
                    MessageAlertIncrement(friend);

                    if (friend.Selected)
                        ScrollChatBox();
                }

                control.OnAccept += delegate(FileTransfer ft)
                {
                    SaveFileDialog dialog = new SaveFileDialog();
                    dialog.FileName = e.FileName;

                    if (dialog.ShowDialog() == true)
                    {
                        ft.Path = dialog.FileName;
                        control.FilePath = dialog.FileName;
                        tox.FileSendControl(ft.FriendNumber, 1, ft.FileNumber, ToxFileControl.Accept, new byte[0]);
                    }

                    transfer.Tag.StartTransfer();
                };

                control.OnDecline += delegate(FileTransfer ft)
                {
                    ft.Kill(false);

                    if (transfers.Contains(ft))
                        transfers.Remove(ft);
                };

                control.OnPause += delegate(FileTransfer ft)
                {
                    if (ft.Paused)
                        tox.FileSendControl(ft.FriendNumber, 1, ft.FileNumber, ToxFileControl.Pause, new byte[0]);
                    else
                        tox.FileSendControl(ft.FriendNumber, 0, ft.FileNumber, ToxFileControl.Accept, new byte[0]);
                };

                control.OnFileOpen += delegate(FileTransfer ft)
                {
                    try { Process.Start(ft.Path); }
                    catch { /*want to open a "choose program dialog" here*/ }
                };

                control.OnFolderOpen += delegate(FileTransfer ft)
                {
                    Process.Start("explorer.exe", @"/select, " + ft.Path);
                };

                transfers.Add(transfer);

                if (ViewModel.MainToxyUser.ToxStatus != ToxUserStatus.Busy)
                    this.Flash();
            })));
        }

        private void tox_OnConnectionStatusChanged(object sender, ToxEventArgs.ConnectionStatusEventArgs e)
        {
            var friend = Dispatcher.Invoke(() => ViewModel.GetFriendObjectByNumber(e.FriendNumber));
            if (friend == null)
                return;

            if (e.Status == ToxFriendConnectionStatus.Offline)
            {
                Dispatcher.BeginInvoke(((Action)(() =>
                {
                    DateTime lastOnline = TimeZoneInfo.ConvertTime(tox.GetLastOnline(e.FriendNumber), TimeZoneInfo.Utc, TimeZoneInfo.Local);

                    if (lastOnline.Year == 1970) //quick and dirty way to check if we're dealing with epoch 0
                        friend.StatusMessage = "Friend request sent";
                    else
                        friend.StatusMessage = string.Format("Last seen: {0} {1}", lastOnline.ToShortDateString(), lastOnline.ToLongTimeString());

                    friend.ToxStatus = ToxUserStatus.Invalid; //not the proper way to do it, I know...

                    if (friend.Selected)
                    {
                        CallButton.Visibility = Visibility.Collapsed;
                        FileButton.Visibility = Visibility.Collapsed;
                        TypingStatusLabel.Content = "";
                    }
                })));

                var receivers = transfers.Where(t => t.GetType() == typeof(FileReceiver) && t.FriendNumber == e.FriendNumber && !t.Finished);
                if (receivers.Count() > 0)
                {
                    foreach (var transfer in receivers)
                        transfer.Broken = true;
                }

                var senders = transfers.Where(t => t.GetType() == typeof(FileSender) && t.FriendNumber == e.FriendNumber && !t.Finished);
                if (senders.Count() > 0)
                {
                    foreach (var transfer in senders)
                        transfer.Broken = true;
                }
            }
            else if (e.Status == ToxFriendConnectionStatus.Online)
            {
                Dispatcher.BeginInvoke(((Action)(() =>
                {
                    friend.StatusMessage = getFriendStatusMessage(friend.ChatNumber);

                    if (friend.Selected)
                    {
                        CallButton.Visibility = Visibility.Visible;
                        FileButton.Visibility = Visibility.Visible;
                    }
                })));

                //kinda ugly to do this every time, I guess we don't really have a choice
                tox.RequestAvatarInfo(e.FriendNumber);

                var receivers = transfers.Where(t => t.GetType() == typeof(FileReceiver) && t.FriendNumber == e.FriendNumber && !t.Finished);
                if (receivers.Count() > 0)
                {
                    foreach (FileReceiver transfer in receivers)
                    {
                        if (transfer.Broken)
                        {
                            tox.FileSendControl(e.FriendNumber, 1, transfer.FileNumber, ToxFileControl.ResumeBroken, BitConverter.GetBytes(transfer.BytesReceived));
                            Debug.WriteLine("File transfer broke, we've received {0} bytes so far", transfer.BytesReceived);
                        }
                    }
                }
            }

            Dispatcher.BeginInvoke(((Action)(() => RearrangeChatList())));
        }

        private void tox_OnTypingChange(object sender, ToxEventArgs.TypingStatusEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend == null)
                    return;

                if (friend.Selected)
                {
                    if (e.IsTyping)
                        TypingStatusLabel.Content = getFriendName(e.FriendNumber) + " is typing...";
                    else
                        TypingStatusLabel.Content = "";
                }
            })));
        }

        private void tox_OnStatusMessage(object sender, ToxEventArgs.StatusMessageEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend != null)
                {
                    friend.StatusMessage = getFriendStatusMessage(e.FriendNumber);
                }
            })));
        }

        private void tox_OnUserStatus(object sender, ToxEventArgs.UserStatusEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend != null)
                {
                    friend.ToxStatus = e.UserStatus;
                }

                RearrangeChatList();
            })));
        }

        private void tox_OnFriendRequest(object sender, ToxEventArgs.FriendRequestEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                try
                {
                    AddFriendRequestToView(e.Id, e.Message);
                    if (ViewModel.MainToxyUser.ToxStatus != ToxUserStatus.Busy)
                        this.Flash();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }

                nIcon.Icon = newMessageNotifyIcon;
                ViewModel.HasNewMessage = true;
            })));
        }

        private void tox_OnFriendAction(object sender, ToxEventArgs.FriendActionEventArgs e)
        {
            MessageData data = new MessageData() { Username = "*  ", Message = string.Format("{0} {1}", getFriendName(e.FriendNumber), e.Action), IsAction = true, Timestamp = DateTime.Now };

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                AddActionToView(e.FriendNumber, data, false);

                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend != null)
                {
                    MessageAlertIncrement(friend);

                    if (friend.Selected)
                        ScrollChatBox();
                }
                if (ViewModel.MainToxyUser.ToxStatus != ToxUserStatus.Busy)
                    this.Flash();

                nIcon.Icon = newMessageNotifyIcon;
                ViewModel.HasNewMessage = true;
            })));

            if (config.EnableChatLogging)
                dbConnection.InsertAsync(new Tables.ToxMessage() { PublicKey = tox.GetClientId(e.FriendNumber).GetString(), Message = data.Message, Timestamp = DateTime.Now, IsAction = true, Name = data.Username, ProfilePublicKey = tox.Id.PublicKey.GetString() });
        }

        private void tox_OnFriendMessage(object sender, ToxEventArgs.FriendMessageEventArgs e)
        {
            MessageData data = new MessageData() { Username = getFriendName(e.FriendNumber), Message = e.Message, Timestamp = DateTime.Now };

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                AddMessageToView(e.FriendNumber, data, false);

                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend != null)
                {
                    MessageAlertIncrement(friend);

                    if (friend.Selected)
                        ScrollChatBox();
                }
                if (ViewModel.MainToxyUser.ToxStatus != ToxUserStatus.Busy)
                    this.Flash();

                nIcon.Icon = newMessageNotifyIcon;
                ViewModel.HasNewMessage = true;
            })));

            if (config.EnableChatLogging)
                dbConnection.InsertAsync(new Tables.ToxMessage() { PublicKey = tox.GetClientId(e.FriendNumber).GetString(), Message = data.Message, Timestamp = DateTime.Now, IsAction = false, Name = data.Username, ProfilePublicKey = tox.Id.PublicKey.GetString() });
        }

        private void tox_OnNameChange(object sender, ToxEventArgs.NameChangeEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var friend = ViewModel.GetFriendObjectByNumber(e.FriendNumber);
                if (friend != null)
                {
                    friend.Name = getFriendName(e.FriendNumber);
                }
            })));
        }

        #endregion

        #region ToxAv EventHandlers

        private void toxav_OnReceivedVideo(object sender, ToxAvEventArgs.VideoDataEventArgs e)
        {
            if (Dispatcher.Invoke(() => (call == null || call.GetType() == typeof(ToxGroupCall) || call.Ended || ViewModel.IsGroupSelected || call.FriendNumber != ViewModel.SelectedChatNumber)))
                return;

            ProcessVideoFrame(e.Frame);
        }

        private void toxav_OnPeerCodecSettingsChanged(object sender, ToxAvEventArgs.CallStateEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                if (call == null || call.GetType() == typeof(ToxGroupCall) || e.CallIndex != call.CallIndex)
                    return;

                if (toxav.GetPeerCodecSettings(e.CallIndex, 0).CallType != ToxAvCallType.Video)
                {
                    VideoImageRow.Height = new GridLength(0);
                    VideoGridSplitter.IsEnabled = false;
                    VideoChatImage.Source = null;
                }
                else if (ViewModel.IsFriendSelected && toxav.GetPeerID(e.CallIndex, 0) == ViewModel.SelectedChatNumber)
                {
                    VideoImageRow.Height = new GridLength(300);
                    VideoGridSplitter.IsEnabled = true;
                }
            })));
        }

        private void toxav_OnReceivedGroupAudio(object sender, ToxAvEventArgs.GroupAudioDataEventArgs e)
        {
            var group = Dispatcher.Invoke(() => ViewModel.GetGroupObjectByNumber(e.GroupNumber));
            if (group == null)
                return;

            var peer = group.PeerList.GetPeerByPeerNumber(e.PeerNumber);
            if (peer == null || peer.Ignored || peer.Muted)
                return;

            if (call != null && call.GetType() == typeof(ToxGroupCall))
                ((ToxGroupCall)call).ProcessAudioFrame(e.Data, e.Channels);
        }

        private void toxav_OnReceivedAudio(object sender, ToxAvEventArgs.AudioDataEventArgs e)
        {
            if (call == null)
                return;

            call.ProcessAudioFrame(e.Data);
        }

        private void toxav_OnEnd(object sender, ToxAvEventArgs.CallStateEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                EndCall();

                CallButton.Visibility = Visibility.Visible;
                HangupButton.Visibility = Visibility.Collapsed;
                VideoButton.Visibility = Visibility.Collapsed;
                VideoButton.IsChecked = false;
            })));
        }

        private void toxav_OnStart(object sender, ToxAvEventArgs.CallStateEventArgs e)
        {
            var settings = toxav.GetPeerCodecSettings(e.CallIndex, 0);

            if (call != null)
                call.Start(config.InputDevice, config.OutputDevice, settings, config.VideoDevice);

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                if (settings.CallType == ToxAvCallType.Video)
                {
                    VideoImageRow.Height = new GridLength(300);
                    VideoGridSplitter.IsEnabled = true;
                }

                int friendnumber = toxav.GetPeerID(e.CallIndex, 0);
                var callingFriend = ViewModel.GetFriendObjectByNumber(friendnumber);
                if (callingFriend != null)
                {
                    callingFriend.IsCalling = false;
                    callingFriend.IsCallingToFriend = false;
                    CallButton.Visibility = Visibility.Collapsed;
                    if (callingFriend.Selected)
                    {
                        HangupButton.Visibility = Visibility.Visible;
                        VideoButton.Visibility = Visibility.Visible;
                    }
                    ViewModel.CallingFriend = callingFriend;
                }
            })));

            call.SetTimerCallback(timerCallback);
        }

        private void timerCallback(object state)
        {
            if (call == null)
                return;

            call.TotalSeconds++;
            var timeSpan = TimeSpan.FromSeconds(call.TotalSeconds);

            Dispatcher.BeginInvoke(((Action)(() => CurrentCallControl.TimerLabel.Content = string.Format("{0}:{1}:{2}", timeSpan.Hours.ToString("00"), timeSpan.Minutes.ToString("00"), timeSpan.Seconds.ToString("00")))));
        }

        private void toxav_OnInvite(object sender, ToxAvEventArgs.CallStateEventArgs e)
        {
            //TODO: notify the user of another incoming call
            if (call != null)
                return;

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var friend = ViewModel.GetFriendObjectByNumber(toxav.GetPeerID(e.CallIndex, 0));
                if (friend != null)
                {
                    friend.CallIndex = e.CallIndex;
                    friend.IsCalling = true;
                }
            })));
        }

        #endregion

        private void RearrangeGroupPeerList(IGroupObject group)
        {
            var peers = new ObservableCollection<GroupPeer>();

            for (int i = 0; i < tox.GetGroupMemberCount(group.ChatNumber); i++)
            {
                var oldPeer = group.PeerList.GetPeerByPeerNumber(i);
                GroupPeer newPeer;

                if (oldPeer != null)
                    newPeer = oldPeer;
                else
                    newPeer = new GroupPeer(group.ChatNumber, i) { Name = tox.GetGroupMemberName(group.ChatNumber, i) };

                peers.Add(newPeer);
            }

            group.PeerList = new GroupPeerCollection(peers.OrderBy(p => p.Name).ToList());
        }

        private void RearrangeChatList()
        {
            ViewModel.UpdateChatCollection(new ObservableCollection<IChatObject>(ViewModel.ChatCollection.OrderBy(chat => chat.GetType() == typeof(GroupControlModelView) ? 3 : getStatusPriority(tox.GetFriendConnectionStatus(chat.ChatNumber), tox.GetUserStatus(chat.ChatNumber))).ThenBy(chat => chat.Name)));
        }

        private int getStatusPriority(ToxFriendConnectionStatus connStatus, ToxUserStatus status)
        {
            if (connStatus == ToxFriendConnectionStatus.Offline)
                return 4;

            switch (status)
            {
                case ToxUserStatus.None:
                    return 0;
                case ToxUserStatus.Away:
                    return 1;
                case ToxUserStatus.Busy:
                    return 2;
                default:
                    return 3;
            }
        }

        private void AddMessageToView(int chatNumber, MessageData data, bool isGroup)
        {
            var dic = isGroup ? groupdic : convdic;

            if (dic.ContainsKey(chatNumber))
            {
                var run = dic[chatNumber].GetLastMessageRun();

                if (run != null && run.Tag.GetType() == typeof(MessageData))
                {
                    if (((MessageData)run.Tag).Username == data.Username)
                        dic[chatNumber].AddNewMessageRow(tox, data, true);
                    else
                        dic[chatNumber].AddNewMessageRow(tox, data, false);
                }
                else
                {
                    dic[chatNumber].AddNewMessageRow(tox, data, false);
                }
            }
            else
            {
                FlowDocument document = FlowDocumentExtensions.CreateNewDocument();
                dic.Add(chatNumber, document);
                dic[chatNumber].AddNewMessageRow(tox, data, false);
            }
        }

        private void AddActionToView(int chatNumber, MessageData data, bool isGroup)
        {
            var dic = isGroup ? groupdic : convdic;

            if (dic.ContainsKey(chatNumber))
            {
                dic[chatNumber].AddNewMessageRow(tox, data, false);
            }
            else
            {
                FlowDocument document = FlowDocumentExtensions.CreateNewDocument();
                dic.Add(chatNumber, document);
                dic[chatNumber].AddNewMessageRow(tox, data, false);
            }
        }

        private async void initDatabase()
        {
            dbConnection = new SQLiteAsyncConnection(dbFilename);
            await dbConnection.CreateTableAsync<Tables.ToxMessage>().ContinueWith((r) => { Console.WriteLine("Created ToxMessage table"); });

            if (config.EnableChatLogging)
            {
                await dbConnection.Table<Tables.ToxMessage>().ToListAsync().ContinueWith((task) =>
                {
                    foreach (Tables.ToxMessage msg in task.Result)
                    {
                        if (string.IsNullOrEmpty(msg.ProfilePublicKey) || msg.ProfilePublicKey != tox.Id.PublicKey.GetString())
                            continue;

                        int friendNumber = GetFriendByPublicKey(msg.PublicKey);
                        if (friendNumber == -1)
                            continue;

                        Dispatcher.BeginInvoke(((Action)(() =>
                        {
                            var messageData = new MessageData() { Username = msg.Name, Message = msg.Message, IsAction = msg.IsAction, IsSelf = msg.IsSelf, Timestamp = msg.Timestamp };

                            if (!msg.IsAction)
                                AddMessageToView(friendNumber, messageData, false);
                            else
                                AddActionToView(friendNumber, messageData, false);
                        })));
                    }
                });
            }
        }

        private int GetFriendByPublicKey(string publicKey)
        {
            var friends = tox.FriendList.Where(num => tox.GetClientId(num).ToString() == publicKey);
            if (friends.Count() != 1)
                return -1;
            else
                return friends.First();
        }

        private void loadAvatars()
        {
            {
                byte[] bytes;
                var avatar = avatarStore.Load(tox.Id.PublicKey, out bytes);
                if (avatar != null && bytes != null && bytes.Length > 0)
                {
                    tox.SetAvatar(ToxAvatarFormat.Png, bytes);
                    ViewModel.MainToxyUser.Avatar = avatar;
                }
            }

            foreach (int friend in tox.FriendList)
            {
                var obj = ViewModel.GetFriendObjectByNumber(friend);
                if (obj == null)
                    continue;

                byte[] bytes;
                var avatar = avatarStore.Load(tox.GetClientId(friend), out bytes);

                if (avatar != null && bytes != null && bytes.Length > 0)
                {
                    obj.AvatarBytes = bytes;
                    obj.Avatar = avatar;
                }
            }
        }

        private Task<bool> applyAvatar(IFriendObject friend, byte[] data)
        {
            return Task.Run(() =>
            {
                Debug.WriteLine(string.Format("Saving avatar to disk ({0})", friend.ChatNumber));
                if (!avatarStore.Save(data, tox.GetClientId(friend.ChatNumber)))
                {
                    Debug.WriteLine(string.Format("Could not save avatar to disk ({0})", friend.ChatNumber));
                    return false;
                }

                MemoryStream stream = new MemoryStream(data);

                using (Bitmap bmp = new Bitmap(stream))
                {
                    var result = bmp.ToBitmapImage(ImageFormat.Png);
                    Dispatcher.BeginInvoke(((Action)(() => friend.Avatar = result)));
                }

                return true;
            });
        }

        private async void Chat_Drop(object sender, DragEventArgs e)
        {
            if (ViewModel.IsGroupSelected)
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop) && tox.IsOnline(ViewModel.SelectedChatNumber))
            {
                var docPath = (string[])e.Data.GetData(DataFormats.FileDrop);
                MetroDialogOptions.ColorScheme = MetroDialogColorScheme.Theme;

                var mySettings = new MetroDialogSettings()
                {
                    AffirmativeButtonText = "Yes",
                    FirstAuxiliaryButtonText = "Cancel",
                    AnimateShow = false,
                    AnimateHide = false,
                    ColorScheme = MetroDialogColorScheme.Theme
                };

                MessageDialogResult result = await this.ShowMessageAsync("Please confirm", "Are you sure you want to send this file?",
                MessageDialogStyle.AffirmativeAndNegative, mySettings);

                if (result == MessageDialogResult.Affirmative)
                {
                    SendFile(ViewModel.SelectedChatNumber, docPath[0]);
                }
            }
        }

        private void Chat_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !ViewModel.IsGroupSelected && tox.IsOnline(ViewModel.SelectedChatNumber))
            {
                e.Effects = DragDropEffects.All;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async Task loadTox()
        {
            if (!Directory.Exists(toxDataDir))
                Directory.CreateDirectory(toxDataDir);

            string[] fileNames = Directory.GetFiles(toxDataDir, "*.tox", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".tox")).ToArray();
            if (fileNames.Length > 0)
            {
                if (!fileNames.Contains(toxDataFilename))
                {
                    SwitchProfileButton_Click(this, new RoutedEventArgs());
                }
                else
                {
                    ToxData data = ToxData.FromDisk(toxDataFilename);
                    if (data == null || !tox.Load(data))
                    {
                        MessageBox.Show("Could not load tox data, this program will now exit.", "Error");
                        Application.Current.Shutdown();
                    }
                }
            }
            else if (File.Exists(toxOldDataFilename))
            {
                string profileName = await this.ShowInputAsync("Old data file", "Toxy has detected an old data file. Please enter a name for your profile");
                if (!string.IsNullOrEmpty(profileName))
                    config.ProfileName = profileName;
                else
                    config.ProfileName = tox.Id.PublicKey.GetString().Substring(0, 10);

                File.Move(toxOldDataFilename, toxDataFilename);
                ConfigTools.Save(config, configFilename);

                ToxData data = ToxData.FromDisk(toxDataFilename);
                if (data == null || !tox.Load(data))
                {
                    MessageBox.Show("Could not load tox data, this program will now exit.", "Error");
                    Close();
                }
            }
            else
            {
                string profileName = await this.ShowInputAsync("Welcome to Toxy!", "To get started, enter a name for your first profile.");
                if (!string.IsNullOrEmpty(profileName))
                    config.ProfileName = profileName;
                else
                    config.ProfileName = tox.Id.PublicKey.GetString().Substring(0, 10);

                tox.Name = config.ProfileName;
                tox.GetData().Save(toxDataFilename);
                ConfigTools.Save(config, configFilename);
            }
        }

        private void applyConfig()
        {
            var accent = ThemeManager.GetAccent(config.AccentColor);
            var theme = ThemeManager.GetAppTheme(config.Theme);

            if (accent != null && theme != null)
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current, accent, theme);

            Width = config.WindowSize.Width;
            Height = config.WindowSize.Height;

            ExecuteActionsOnNotifyIcon();
        }

        private void InitializeNotifyIcon()
        {
            Stream newMessageIconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Toxy;component/Resources/Icons/icon2.ico")).Stream;
            Stream iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Toxy;component/Resources/Icons/icon.ico")).Stream;

            notifyIcon = new Icon(iconStream);
            newMessageNotifyIcon = new Icon(newMessageIconStream);

            nIcon.Icon = notifyIcon;
            nIcon.MouseClick += nIcon_MouseClick;

            var trayIconContextMenu = new System.Windows.Forms.ContextMenu();
            var closeMenuItem = new System.Windows.Forms.MenuItem("Exit", closeMenuItem_Click);
            var openMenuItem = new System.Windows.Forms.MenuItem("Open", openMenuItem_Click);

            var statusMenuItem = new System.Windows.Forms.MenuItem("Status");
            var setOnlineMenuItem = new System.Windows.Forms.MenuItem("Online", setStatusMenuItem_Click);
            var setAwayMenuItem = new System.Windows.Forms.MenuItem("Away", setStatusMenuItem_Click);
            var setBusyMenuItem = new System.Windows.Forms.MenuItem("Busy", setStatusMenuItem_Click);

            setOnlineMenuItem.Tag = 0; // Online
            setAwayMenuItem.Tag = 1; // Away
            setBusyMenuItem.Tag = 2; // Busy

            statusMenuItem.MenuItems.Add(setOnlineMenuItem);
            statusMenuItem.MenuItems.Add(setAwayMenuItem);
            statusMenuItem.MenuItems.Add(setBusyMenuItem);

            trayIconContextMenu.MenuItems.Add(openMenuItem);
            trayIconContextMenu.MenuItems.Add(statusMenuItem);
            trayIconContextMenu.MenuItems.Add(closeMenuItem);
            nIcon.ContextMenu = trayIconContextMenu;
        }

        private void nIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left)
                return;

            if (WindowState != WindowState.Normal)
            {
                Show();
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
            }
            else
            {
                Hide();
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
            }
        }

        private void setStatusMenuItem_Click(object sender, EventArgs eventArgs)
        {
            if (tox.IsConnected)
                SetStatus((ToxUserStatus)((System.Windows.Forms.MenuItem)sender).Tag, true);
        }

        private void openMenuItem_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
        }

        private void closeMenuItem_Click(object sender, EventArgs eventArgs)
        {
            forceClose = true;
            Close();
        }

        public MainWindowViewModel ViewModel
        {
            get { return DataContext as MainWindowViewModel; }
        }

        private FileTransfer GetFileTransfer(int friendnumber, int filenumber)
        {
            foreach (FileTransfer ft in transfers)
                if (ft.FileNumber == filenumber && ft.FriendNumber == friendnumber && !ft.Finished)
                    return ft;

            return null;
        }

        private void ScrollChatBox()
        {
            ScrollViewer viewer = ChatBox.FindScrollViewer();

            if (viewer != null)
                if (viewer.ScrollableHeight == viewer.VerticalOffset)
                    viewer.ScrollToBottom();
        }

        private void NewGroupButton_Click(object sender, RoutedEventArgs e)
        {
            GroupContextMenu.PlacementTarget = this;
            GroupContextMenu.IsOpen = true;
        }

        private void InitFriends()
        {
            //Creates a new FriendControl for every friend
            foreach (var friendNumber in tox.FriendList)
            {
                AddFriendToView(friendNumber, false);
            }
        }

        private IGroupObject AddGroupToView(int groupnumber, ToxGroupType type)
        {
            string groupname = string.Format("Groupchat #{0}", groupnumber);

            if (type == ToxGroupType.Av)
                groupname += " \uD83D\uDD0A"; /*:loud_sound:*/

            var groupMV = new GroupControlModelView();
            groupMV.ChatNumber = groupnumber;
            groupMV.Name = groupname;
            groupMV.GroupType = type;
            groupMV.StatusMessage = string.Format("Peers online: {0}", tox.GetGroupMemberCount(groupnumber));//string.Join(", ", tox.GetGroupNames(groupnumber));
            groupMV.SelectedAction = GroupSelectedAction;
            groupMV.DeleteAction = GroupDeleteAction;
            groupMV.ChangeTitleAction = ChangeTitleAction;

            ViewModel.ChatCollection.Add(groupMV);
            RearrangeChatList();

            return groupMV;
        }

        private async void ChangeTitleAction(IGroupObject groupObject)
        {
            string title = await this.ShowInputAsync("Change group title", "Enter a new title for this group.", new MetroDialogSettings() { DefaultText = tox.GetGroupTopic(groupObject.ChatNumber) });
            if (string.IsNullOrEmpty(title))
                return;

            if (tox.SetGroupTopic(groupObject.ChatNumber, title))
            {
                groupObject.Name = title;
                groupObject.AdditionalInfo = string.Format("Topic set by: {0}", tox.Name);
            }
        }

        private void GroupDeleteAction(IGroupObject groupObject)
        {
            ViewModel.ChatCollection.Remove(groupObject);
            int groupNumber = groupObject.ChatNumber;
            if (groupdic.ContainsKey(groupNumber))
            {
                groupdic.Remove(groupNumber);

                if (groupObject.Selected)
                    ChatBox.Document = null;
            }

            /*if (tox.GetGroupType(groupNumber) == ToxGroupType.Av && call != null)
            {
                call.Stop();
                call = null;
            }*/

            tox.DeleteGroupChat(groupNumber, "rip alnf");

            groupObject.SelectedAction = null;
            groupObject.DeleteAction = null;

            MicButton.IsChecked = false;
        }

        private void GroupSelectedAction(IGroupObject groupObject, bool isSelected)
        {
            MessageAlertClear(groupObject);

            TypingStatusLabel.Content = "";

            if (isSelected)
            {
                SelectGroupControl(groupObject);
                ScrollChatBox();
                TextToSend.Focus();
            }
        }

        private string getFriendName(int friendnumber)
        {
            return tox.GetName(friendnumber).Replace("\n", "").Replace("\r", "");
        }

        private string getSelfStatusMessage()
        {
            return tox.StatusMessage.Replace("\n", "").Replace("\r", "");
        }

        private string getSelfName()
        {
            return tox.Name.Replace("\n", "").Replace("\r", "");
        }

        private string getFriendStatusMessage(int friendnumber)
        {
            return tox.GetStatusMessage(friendnumber).Replace("\n", "").Replace("\r", "");
        }

        private void AddFriendToView(int friendNumber, bool sentRequest)
        {
            string friendStatus = "";
            if (tox.IsOnline(friendNumber))
            {
                friendStatus = getFriendStatusMessage(friendNumber);
            }
            else
            {
                DateTime lastOnline = TimeZoneInfo.ConvertTime(tox.GetLastOnline(friendNumber), TimeZoneInfo.Utc, TimeZoneInfo.Local);

                if (lastOnline.Year == 1970)
                {
                    if (sentRequest)
                        friendStatus = "Friend request sent";
                }
                else
                    friendStatus = string.Format("Last seen: {0} {1}", lastOnline.ToShortDateString(), lastOnline.ToLongTimeString());
            }

            string friendName = getFriendName(friendNumber);
            if (string.IsNullOrEmpty(friendName))
            {
                friendName = tox.GetClientId(friendNumber).GetString();
            }

            var friendMV = new FriendControlModelView(ViewModel);
            friendMV.ChatNumber = friendNumber;
            friendMV.Name = friendName;
            friendMV.StatusMessage = friendStatus;
            friendMV.ToxStatus = ToxUserStatus.Invalid;
            friendMV.SelectedAction = FriendSelectedAction;
            friendMV.DenyCallAction = FriendDenyCallAction;
            friendMV.AcceptCallAction = FriendAcceptCallAction;
            friendMV.CopyIDAction = FriendCopyIdAction;
            friendMV.DeleteAction = FriendDeleteAction;
            friendMV.GroupInviteAction = GroupInviteAction;
            friendMV.HangupAction = FriendHangupAction;

            ViewModel.ChatCollection.Add(friendMV);
            RearrangeChatList();
        }

        private void FriendHangupAction(IFriendObject friendObject)
        {
            EndCall(friendObject);
        }

        private void GroupInviteAction(IFriendObject friendObject, IGroupObject groupObject)
        {
            //tox.InviteFriend(friendObject.ChatNumber, groupObject.ChatNumber);
        }

        private void FriendDeleteAction(IFriendObject friendObject)
        {
            ViewModel.ChatCollection.Remove(friendObject);
            var friendNumber = friendObject.ChatNumber;
            if (convdic.ContainsKey(friendNumber))
            {
                convdic.Remove(friendNumber);
                if (friendObject.Selected)
                {
                    ChatBox.Document = null;
                }
            }
            tox.DeleteFriend(friendNumber);
            friendObject.SelectedAction = null;
            friendObject.DenyCallAction = null;
            friendObject.AcceptCallAction = null;
            friendObject.CopyIDAction = null;
            friendObject.DeleteAction = null;
            friendObject.GroupInviteAction = null;
            friendObject.MainViewModel = null;

            saveTox();
        }

        private void saveTox()
        {
            if (!config.Portable)
            {
                if (!Directory.Exists(toxDataDir))
                    Directory.CreateDirectory(toxDataDir);
            }

            ToxData data = tox.GetData();
            if (data != null)
                data.Save(toxDataFilename);
        }

        private void FriendCopyIdAction(IFriendObject friendObject)
        {
            Clipboard.Clear();
            Clipboard.SetText(tox.GetClientId(friendObject.ChatNumber).GetString());
        }

        private void FriendSelectedAction(IFriendObject friendObject, bool isSelected)
        {
            MessageAlertClear(friendObject);

            if (isSelected)
            {
                if (!tox.GetIsTyping(friendObject.ChatNumber) || tox.GetUserStatus(friendObject.ChatNumber) == ToxUserStatus.None)
                    TypingStatusLabel.Content = "";
                else
                    TypingStatusLabel.Content = getFriendName(friendObject.ChatNumber) + " is typing...";

                SelectFriendControl(friendObject);
                ScrollChatBox();
                TextToSend.Focus();
            }
        }

        private void FriendAcceptCallAction(IFriendObject friendObject)
        {
            if (call != null)
                return;

            call = new ToxCall(toxav, friendObject.CallIndex, friendObject.ChatNumber);
            call.FilterAudio = config.FilterAudio;
            call.Answer();
        }

        private void FriendDenyCallAction(IFriendObject friendObject)
        {
            if (call == null)
            {
                toxav.Reject(friendObject.CallIndex, "I'm busy...");
                friendObject.IsCalling = false;
            }
            else
            {
                call.Stop();
                call = null;
            }
        }

        private void AddFriendRequestToView(string id, string message)
        {
            var friendMV = new FriendControlModelView(ViewModel);
            friendMV.IsRequest = true;
            friendMV.Name = id;
            friendMV.ToxStatus = ToxUserStatus.Invalid;
            friendMV.RequestMessageData = new MessageData() { Message = message, Username = "Request Message", Timestamp = DateTime.Now };
            friendMV.RequestFlowDocument = FlowDocumentExtensions.CreateNewDocument();
            friendMV.SelectedAction = FriendRequestSelectedAction;
            friendMV.AcceptAction = FriendRequestAcceptAction;
            friendMV.DeclineAction = FriendRequestDeclineAction;

            ViewModel.ChatRequestCollection.Add(friendMV);

            if (ListViewTabControl.SelectedIndex != 1)
            {
                RequestsTabItem.Header = "Requests*";
            }
        }

        private void FriendRequestSelectedAction(IFriendObject friendObject, bool isSelected)
        {
            friendObject.RequestFlowDocument.AddNewMessageRow(tox, friendObject.RequestMessageData, false);
        }

        private void FriendRequestAcceptAction(IFriendObject friendObject)
        {
            int friendnumber = tox.AddFriendNoRequest(new ToxKey(ToxKeyType.Public, friendObject.Name));

            if (friendnumber != -1)
                AddFriendToView(friendnumber, false);
            else
                this.ShowMessageAsync("Unknown Error", "Could not accept friend request.");

            ViewModel.ChatRequestCollection.Remove(friendObject);
            friendObject.RequestFlowDocument = null;
            friendObject.SelectedAction = null;
            friendObject.AcceptAction = null;
            friendObject.DeclineAction = null;
            friendObject.MainViewModel = null;

            saveTox();
        }

        private void FriendRequestDeclineAction(IFriendObject friendObject)
        {
            ViewModel.ChatRequestCollection.Remove(friendObject);
            friendObject.RequestFlowDocument = null;
            friendObject.SelectedAction = null;
            friendObject.AcceptAction = null;
            friendObject.DeclineAction = null;
        }

        private void SelectGroupControl(IGroupObject group)
        {
            if (group == null)
            {
                return;
            }

            CallButton.Visibility = Visibility.Collapsed;
            FileButton.Visibility = Visibility.Collapsed;
            HangupButton.Visibility = Visibility.Collapsed;
            VideoButton.Visibility = Visibility.Collapsed;

            //if (tox.GetGroupType(group.ChatNumber) == ToxGroupType.Av)
                //MicButton.Visibility = Visibility.Visible;
            //else
                MicButton.Visibility = Visibility.Collapsed;

            if (groupdic.ContainsKey(group.ChatNumber))
            {
                ChatBox.Document = groupdic[group.ChatNumber];
            }
            else
            {
                FlowDocument document = FlowDocumentExtensions.CreateNewDocument();
                groupdic.Add(group.ChatNumber, document);
                ChatBox.Document = groupdic[group.ChatNumber];
            }

            VideoImageRow.Height = new GridLength(0);
            VideoGridSplitter.IsEnabled = false;
            VideoChatImage.Source = null;

            GroupListGrid.Visibility = System.Windows.Visibility.Visible;
            PeerColumn.Width = new GridLength(150);
        }

        private void EndCall()
        {
            if (call != null)
            {
                var friendnumber = toxav.GetPeerID(call.CallIndex, 0);
                var friend = ViewModel.GetFriendObjectByNumber(friendnumber);

                EndCall(friend);
            }
            else
            {
                EndCall(null);
            }
        }

        private void EndCall(IFriendObject friend)
        {
            if (friend != null)
            {
                toxav.Cancel(friend.CallIndex, friend.ChatNumber, "I'm busy...");

                friend.IsCalling = false;
                friend.IsCallingToFriend = false;
            }

            if (call != null)
            {
                call.Stop();
                call = null;

                CurrentCallControl.TimerLabel.Content = "00:00:00";
            }

            ViewModel.CallingFriend = null;
            VideoImageRow.Height = new GridLength(0);
            VideoGridSplitter.IsEnabled = false;
            VideoChatImage.Source = null;

            HangupButton.Visibility = Visibility.Collapsed;
            VideoButton.Visibility = Visibility.Collapsed;
            CallButton.Visibility = Visibility.Visible;
        }

        private void SelectFriendControl(IFriendObject friend)
        {
            if (friend == null)
            {
                return;
            }
            int friendNumber = friend.ChatNumber;

            if (call != null && call.GetType() != typeof(ToxGroupCall))
            {
                if (call.FriendNumber != friendNumber)
                {
                    HangupButton.Visibility = Visibility.Collapsed;
                    VideoButton.Visibility = Visibility.Collapsed;

                    if (tox.IsOnline(friendNumber))
                    {
                        CallButton.Visibility = Visibility.Visible;
                        FileButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        CallButton.Visibility = Visibility.Collapsed;
                        FileButton.Visibility = Visibility.Collapsed;
                    }

                    VideoImageRow.Height = new GridLength(0);
                    VideoGridSplitter.IsEnabled = false;
                    VideoChatImage.Source = null;
                }
                else
                {
                    HangupButton.Visibility = Visibility.Visible;
                    VideoButton.Visibility = Visibility.Visible;
                    CallButton.Visibility = Visibility.Collapsed;

                    if (toxav.GetPeerCodecSettings(call.CallIndex, 0).CallType == ToxAvCallType.Video)
                    {
                        VideoImageRow.Height = new GridLength(300);
                        VideoGridSplitter.IsEnabled = true;
                    }
                }
            }
            else
            {
                if (!tox.IsOnline(friendNumber))
                {
                    CallButton.Visibility = Visibility.Collapsed;
                    FileButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CallButton.Visibility = Visibility.Visible;
                    FileButton.Visibility = Visibility.Visible;
                }

                VideoImageRow.Height = GridLength.Auto;
            }

            MicButton.Visibility = Visibility.Collapsed;

            if (convdic.ContainsKey(friend.ChatNumber))
            {
                ChatBox.Document = convdic[friend.ChatNumber];
            }
            else
            {
                FlowDocument document = FlowDocumentExtensions.CreateNewDocument();
                convdic.Add(friend.ChatNumber, document);
                ChatBox.Document = convdic[friend.ChatNumber];
            }

            GroupListGrid.Visibility = System.Windows.Visibility.Collapsed;
            PeerColumn.Width = GridLength.Auto;
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (config.HideInTray && !forceClose)
            {
                e.Cancel = true;
                ShowInTaskbar = false;
                WindowState = WindowState.Minimized;
                Hide();
            }
            else
            {
                KillTox(true);
                nIcon.Dispose();
            }
        }

        private void KillTox(bool save)
        {
            if (call != null)
            {
                call.Stop();
                call = null;
            }

            foreach (FileTransfer transfer in transfers)
                transfer.Kill(false);

            convdic.Clear();
            groupdic.Clear();
            transfers.Clear();

            if (toxav != null)
                toxav.Dispose();

            if (tox != null)
            {
                if (save)
                    saveTox();

                tox.Dispose();
            }

            if (config != null)
            {
                config.WindowSize = new System.Windows.Size(this.Width, this.Height);
                ConfigTools.Save(config, configFilename);
            }
        }

        private void OpenAddFriend_Click(object sender, RoutedEventArgs e)
        {
            FriendFlyout.IsOpen = !FriendFlyout.IsOpen;
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingsFlyout.IsOpen)
            {
                SettingsUsername.Text = getSelfName();
                SettingsStatus.Text = getSelfStatusMessage();
                SettingsNospam.Text = tox.GetNospam().ToString();

                Tuple<AppTheme, Accent> style = ThemeManager.DetectAppStyle(System.Windows.Application.Current);
                Accent accent = ThemeManager.GetAccent(style.Item2.Name);
                oldAccent = accent;
                if (accent != null)
                    AccentComboBox.SelectedItem = AccentComboBox.Items.Cast<AccentColorMenuData>().Single(a => a.Name == style.Item2.Name);

                AppTheme theme = ThemeManager.GetAppTheme(style.Item1.Name);
                oldAppTheme = theme;
                if (theme != null)
                    AppThemeComboBox.SelectedItem = AppThemeComboBox.Items.Cast<AppThemeMenuData>().Single(a => a.Name == style.Item1.Name);

                ViewModel.UpdateDevices();

                foreach(var item in VideoDevicesComboBox.Items)
                {
                    var device = (VideoDeviceMenuData)item;
                    if (device.Name == config.VideoDevice)
                    {
                        VideoDevicesComboBox.SelectedItem = item;
                        break;
                    }
                }

                if (InputDevicesComboBox.Items.Count - 1 >= config.InputDevice)
                    InputDevicesComboBox.SelectedIndex = config.InputDevice;

                if (OutputDevicesComboBox.Items.Count - 1 >= config.OutputDevice)
                    OutputDevicesComboBox.SelectedIndex = config.OutputDevice;

                ChatLogCheckBox.IsChecked = config.EnableChatLogging;
                HideInTrayCheckBox.IsChecked = config.HideInTray;
                PortableCheckBox.IsChecked = config.Portable;
                AudioNotificationCheckBox.IsChecked = config.EnableAudioNotifications;
                AlwaysNotifyCheckBox.IsChecked = config.AlwaysNotify;
                FilterAudioCheckbox.IsChecked = config.FilterAudio;

                if (!string.IsNullOrEmpty(config.ProxyAddress))
                    SettingsProxyAddress.Text = config.ProxyAddress;

                if (config.ProxyPort != 0)
                    SettingsProxyPort.Text = config.ProxyPort.ToString();

                foreach (ComboBoxItem item in ProxyTypeComboBox.Items)
                {
                    if ((ToxProxyType)int.Parse((string)item.Tag) == config.ProxyType)
                    {
                        ProxyTypeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            SettingsFlyout.IsOpen = !SettingsFlyout.IsOpen;
        }

        private async void AddFriend_Click(object sender, RoutedEventArgs e)
        {
            TextRange message = new TextRange(AddFriendMessage.Document.ContentStart, AddFriendMessage.Document.ContentEnd);

            if (string.IsNullOrWhiteSpace(AddFriendID.Text) || message.Text == null)
                return;

            string friendID = AddFriendID.Text.Trim();
            int tries = 0;

            if (friendID.Contains("@"))
            {
                if (config.ProxyType != ToxProxyType.None && config.RemindAboutProxy)
                {
                    MessageDialogResult result = await this.ShowMessageAsync("Warning", "You're about to submit a dns lookup query, the configured proxy will not be used for this.\nDo you wish to continue?", MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, new MetroDialogSettings() { AffirmativeButtonText = "Yes, don't remind me again", NegativeButtonText = "Yes", FirstAuxiliaryButtonText = "No" });
                    if (result == MessageDialogResult.Affirmative)
                    {
                        config.RemindAboutProxy = false;
                        ConfigTools.Save(config, configFilename);
                    }
                    else if (result == MessageDialogResult.FirstAuxiliary)
                    {
                        return;
                    }
                }

            discover:
                try
                {
                    tries++;
                    string id = DnsTools.DiscoverToxID(friendID, config.NameServices, config.OnlyUseLocalNameServiceStore);

                    if (string.IsNullOrEmpty(id))
                        throw new Exception("The server returned an empty result");

                    AddFriendID.Text = id;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Could not resolve {0}: {1}", friendID, ex.ToString()));

                    if (tries < 3)
                        goto discover;

                    this.ShowMessageAsync("Could not find a tox id", ex.Message.ToString());
                }

                return;
            }

            try
            {
                int friendnumber = tox.AddFriend(new ToxId(friendID), message.Text);
                FriendFlyout.IsOpen = false;
                AddFriendToView(friendnumber, true);
            }
            catch (ToxAFException ex)
            {
                if (ex.Error != ToxAFError.SetNewNospam)
                    this.ShowMessageAsync("An error occurred", Tools.GetAFError(ex.Error));

                return;
            }
            catch
            {
                this.ShowMessageAsync("An error occurred", "The ID you entered is not valid.");
                return;
            }

            AddFriendID.Text = string.Empty;
            AddFriendMessage.Document.Blocks.Clear();
            AddFriendMessage.Document.Blocks.Add(new Paragraph(new Run("Hello, I'd like to add you to my friends list.")));

            saveTox();
            FriendFlyout.IsOpen = false;
        }

        private void SaveSettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            tox.Name = SettingsUsername.Text;
            tox.StatusMessage = SettingsStatus.Text;

            uint nospam;
            if (uint.TryParse(SettingsNospam.Text, out nospam))
                tox.SetNospam(nospam);

            ViewModel.MainToxyUser.Name = getSelfName();
            ViewModel.MainToxyUser.StatusMessage = getSelfStatusMessage();

            config.HideInTray = (bool)HideInTrayCheckBox.IsChecked;

            SettingsFlyout.IsOpen = false;

            if (AccentComboBox.SelectedItem != null)
            {
                string accentName = ((AccentColorMenuData)AccentComboBox.SelectedItem).Name;
                var theme = ThemeManager.DetectAppStyle(System.Windows.Application.Current);
                var accent = ThemeManager.GetAccent(accentName);
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current, accent, theme.Item1);

                config.AccentColor = accentName;
            }

            if (AppThemeComboBox.SelectedItem != null)
            {
                string themeName = ((AppThemeMenuData)AppThemeComboBox.SelectedItem).Name;
                var theme = ThemeManager.DetectAppStyle(System.Windows.Application.Current);
                var appTheme = ThemeManager.GetAppTheme(themeName);
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current, theme.Item2, appTheme);

                config.Theme = themeName;
            }

            int index = InputDevicesComboBox.SelectedIndex;
            if (WaveIn.DeviceCount > 0 && WaveIn.DeviceCount >= index)
            {
                if (config.InputDevice != index)
                    if (call != null)
                        call.SwitchInputDevice(index);

                config.InputDevice = index;
            }

            index = OutputDevicesComboBox.SelectedIndex;
            if (WaveOut.DeviceCount > 0 && WaveOut.DeviceCount >= index)
            {
                if (config.OutputDevice != index)
                    if (call != null)
                        call.SwitchOutputDevice(index);

                config.OutputDevice = index;
            }

            if (VideoDevicesComboBox.SelectedItem != null)
                config.VideoDevice = ((VideoDeviceMenuData)VideoDevicesComboBox.SelectedItem).Name;

            config.EnableChatLogging = (bool)ChatLogCheckBox.IsChecked;
            config.Portable = (bool)PortableCheckBox.IsChecked;
            config.EnableAudioNotifications = (bool)AudioNotificationCheckBox.IsChecked;
            config.AlwaysNotify = (bool)AlwaysNotifyCheckBox.IsChecked;
            ExecuteActionsOnNotifyIcon();

            bool filterAudio = (bool)FilterAudioCheckbox.IsChecked;

            if (config.FilterAudio != filterAudio)
                if (call != null)
                    call.FilterAudio = filterAudio;

            config.FilterAudio = filterAudio;

            bool proxyConfigChanged = false;
            var proxyType = (ToxProxyType)int.Parse((string)((ComboBoxItem)ProxyTypeComboBox.SelectedItem).Tag);

            if (config.ProxyType != proxyType || config.ProxyAddress != SettingsProxyAddress.Text || config.ProxyPort.ToString() != SettingsProxyPort.Text)
                proxyConfigChanged = true;

            config.ProxyType = proxyType;
            config.ProxyAddress = SettingsProxyAddress.Text;

            int proxyPort;
            if (int.TryParse(SettingsProxyPort.Text, out proxyPort))
                config.ProxyPort = proxyPort;

            ConfigTools.Save(config, configFilename);
            saveTox();

            savingSettings = true;

            if (proxyConfigChanged)
            {
                this.ShowMessageAsync("Alert", "You have changed your proxy configuration.\nPlease restart Toxy to apply these changes.");
            }
        }

        private void TextToSend_KeyDown(object sender, KeyEventArgs e)
        {
            string text = TextToSend.Text;

            if (e.Key == Key.Enter)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    return;

                if (e.IsRepeat)
                    return;

                if (string.IsNullOrEmpty(text))
                    return;

                var selectedChatNumber = ViewModel.SelectedChatNumber;
                if (!tox.IsOnline(selectedChatNumber) && ViewModel.IsFriendSelected)
                {
                    MessageData data = new MessageData() { Username = getSelfName(), Message = "Your Friend is not online", Id = 0, IsSelf = true, Timestamp = DateTime.Now };

                    if (ViewModel.IsFriendSelected)
                    {
                        AddMessageToView(selectedChatNumber, data, false);
                    }

                    return;
                }

                if (text.StartsWith("/me "))
                {
                    //action
                    string action = text.Substring(4);
                    int messageid = -1;

                    if (ViewModel.IsFriendSelected)
                        messageid = tox.SendAction(selectedChatNumber, action);
                    else if (ViewModel.IsGroupSelected)
                        tox.SendGroupAction(selectedChatNumber, action);

                    MessageData data = new MessageData() { Username = "*  ", Message = string.Format("{0} {1}", getSelfName(), action), IsAction = true, Id = messageid, IsSelf = true, IsGroupMsg = ViewModel.IsGroupSelected, Timestamp = DateTime.Now };

                    if (ViewModel.IsFriendSelected)
                    {
                        AddActionToView(selectedChatNumber, data, false);

                        if (config.EnableChatLogging)
                            dbConnection.InsertAsync(new Tables.ToxMessage() { PublicKey = tox.GetClientId(selectedChatNumber).GetString(), Message = data.Message, Timestamp = DateTime.Now, IsAction = true, Name = data.Username, ProfilePublicKey = tox.Id.PublicKey.GetString() });
                    }
                    else
                    {
                        AddActionToView(selectedChatNumber, data, true);
                    }
                }
                else
                {
                    //regular message
                    foreach (string message in text.WordWrap(ToxConstants.MaxMessageLength))
                    {
                        int messageid = -1;

                        if (ViewModel.IsFriendSelected)
                            messageid = tox.SendMessage(selectedChatNumber, message);
                        else if (ViewModel.IsGroupSelected)
                            tox.SendGroupMessage(selectedChatNumber, message);

                        MessageData data = new MessageData() { Username = getSelfName(), Message = message, Id = messageid, IsSelf = true, IsGroupMsg = ViewModel.IsGroupSelected, Timestamp = DateTime.Now };

                        if (ViewModel.IsFriendSelected)
                        {
                            AddMessageToView(selectedChatNumber, data, false);

                            if (config.EnableChatLogging)
                                dbConnection.InsertAsync(new Tables.ToxMessage() { PublicKey = tox.GetClientId(selectedChatNumber).GetString(), Message = data.Message, Timestamp = DateTime.Now, IsAction = false, Name = data.Username, ProfilePublicKey = tox.Id.PublicKey.GetString() });
                        }
                        else
                        {
                            AddMessageToView(selectedChatNumber, data, true);
                        }
                    }
                }

                ScrollChatBox();

                TextToSend.Text = string.Empty;
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && ViewModel.IsGroupSelected)
            {
                string[] names = tox.GetGroupNames(ViewModel.SelectedChatNumber);

                foreach (string name in names)
                {
                    string lastPart = text.Split(' ').Last();
                    if (!name.ToLower().StartsWith(lastPart.ToLower()))
                        continue;

                    if (text.Split(' ').Length > 1)
                    {
                        if (text.Last() != ' ')
                        {
                            TextToSend.Text = string.Format("{0}{1} ", text.Substring(0, text.Length - lastPart.Length), name);
                            TextToSend.SelectionStart = TextToSend.Text.Length;
                        }
                    }
                    else
                    {
                        TextToSend.Text = string.Format("{0}, ", name);
                        TextToSend.SelectionStart = TextToSend.Text.Length;
                    }
                }

                e.Handled = true;
            }
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/Reverp/Toxy");
        }

        private void TextToSend_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!ViewModel.IsFriendSelected)
                return;

            string text = TextToSend.Text;

            if (string.IsNullOrEmpty(text))
            {
                if (typing)
                {
                    typing = false;
                    tox.SetUserIsTyping(ViewModel.SelectedChatNumber, typing);
                }
            }
            else
            {
                if (!typing)
                {
                    typing = true;
                    tox.SetUserIsTyping(ViewModel.SelectedChatNumber, typing);
                }
            }
        }

        private void CopyIDButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(tox.Id.ToString());
        }

        private void MetroWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!resizing && focusTextbox)
                TextToSend.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
        }

        private void TextToSend_OnGotFocus(object sender, RoutedEventArgs e)
        {
            focusTextbox = true;
        }

        private void TextToSend_OnLostFocus(object sender, RoutedEventArgs e)
        {
            focusTextbox = false;
        }

        private void Grid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (resizing)
            {
                resizing = false;
                if (focusTextbox)
                {
                    TextToSend.Focus();
                    focusTextbox = false;
                }
            }
        }

        private void OnlineThumbButton_Click(object sender, EventArgs e)
        {
            SetStatus(ToxUserStatus.None, true);
        }

        private void AwayThumbButton_Click(object sender, EventArgs e)
        {
            SetStatus(ToxUserStatus.Away, true);
        }

        private void BusyThumbButton_Click(object sender, EventArgs e)
        {
            SetStatus(ToxUserStatus.Busy, true);
        }

        private void ListViewTabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RequestsTabItem.IsSelected)
                RequestsTabItem.Header = "Requests";
        }

        private void StatusRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            StatusContextMenu.PlacementTarget = this;
            StatusContextMenu.IsOpen = true;
        }

        private void MenuItem_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (!tox.IsConnected)
                return;

            MenuItem menuItem = (MenuItem)e.Source;
            SetStatus((ToxUserStatus)int.Parse(menuItem.Tag.ToString()), true);
        }

        private void TextToSend_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (ViewModel.IsGroupSelected)
                    return;

                if (Clipboard.ContainsImage())
                {
                    var bmp = (Bitmap)System.Windows.Forms.Clipboard.GetImage();
                    byte[] bytes = bmp.GetBytes();

                    if (!convdic.ContainsKey(ViewModel.SelectedChatNumber))
                        convdic.Add(ViewModel.SelectedChatNumber, FlowDocumentExtensions.CreateNewDocument());

                    int filenumber = tox.NewFileSender(ViewModel.SelectedChatNumber, (ulong)bytes.Length, "image.bmp");

                    if (filenumber == -1)
                        return;

                    var transfer = new FileSender(tox, filenumber, ViewModel.SelectedChatNumber, bytes.Length, "image.bmp", new MemoryStream(bytes));
                    var control = convdic[ViewModel.SelectedChatNumber].AddNewFileTransfer(tox, transfer);
                    transfer.Tag = control;

                    transfer.Tag.SetStatus(string.Format("Waiting for {0} to accept...", getFriendName(ViewModel.SelectedChatNumber)));
                    transfer.Tag.AcceptButton.Visibility = Visibility.Collapsed;
                    transfer.Tag.DeclineButton.Visibility = Visibility.Visible;

                    control.OnDecline += delegate(FileTransfer ft)
                    {
                        ft.Kill(false);

                        if (transfers.Contains(ft))
                            transfers.Remove(ft);
                    };

                    control.OnPause += delegate(FileTransfer ft)
                    {
                        if (ft.Paused)
                            tox.FileSendControl(ft.FriendNumber, 1, ft.FileNumber, ToxFileControl.Pause, new byte[0]);
                        else
                            tox.FileSendControl(ft.FriendNumber, 0, ft.FileNumber, ToxFileControl.Accept, new byte[0]);
                    };

                    transfers.Add(transfer);

                    ScrollChatBox();
                }
            }
        }

        private void SetStatus(ToxUserStatus? newStatus, bool changeUserStatus)
        {
            if (newStatus == null)
            {
                newStatus = ToxUserStatus.Invalid;
            }
            else
            {
                if (changeUserStatus)
                {
                    tox.Status = newStatus.GetValueOrDefault();

                    if (tox.Status != newStatus.GetValueOrDefault())
                        return;
                }
            }

            Dispatcher.BeginInvoke(((Action)(() => ViewModel.MainToxyUser.ToxStatus = newStatus.GetValueOrDefault())));
        }

        private void CallButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsFriendSelected)
                return;

            if (call != null)
                return;

            var selectedChatNumber = ViewModel.SelectedChatNumber;
            if (!tox.IsOnline(selectedChatNumber))
                return;

            int call_index;
            ToxAvError error = toxav.Call(selectedChatNumber, ToxAv.DefaultCodecSettings, 30, out call_index);
            if (error != ToxAvError.None)
                return;

            int friendnumber = toxav.GetPeerID(call_index, 0);
            call = new ToxCall(toxav, call_index, friendnumber);
            call.FilterAudio = config.FilterAudio;

            CallButton.Visibility = Visibility.Collapsed;
            HangupButton.Visibility = Visibility.Visible;
            VideoButton.Visibility = Visibility.Visible;

            var callingFriend = ViewModel.GetFriendObjectByNumber(friendnumber);
            if (callingFriend != null)
            {
                ViewModel.CallingFriend = callingFriend;
                callingFriend.IsCallingToFriend = true;
            }
        }

        private void MainHangupButton_OnClick(object sender, RoutedEventArgs e)
        {
            EndCall();
        }

        private void FileButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsFriendSelected)
                return;

            var selectedChatNumber = ViewModel.SelectedChatNumber;
            if (!tox.IsOnline(selectedChatNumber))
                return;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.InitialDirectory = Environment.CurrentDirectory;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != true)
                return;

            string filename = dialog.FileName;

            SendFile(selectedChatNumber, filename);
        }

        private void SendFile(int chatNumber, string filename)
        {
            FileInfo info = new FileInfo(filename);
            int filenumber = tox.NewFileSender(chatNumber, (ulong)info.Length, filename.Split('\\').Last<string>());

            if (filenumber == -1)
                return;

            var transfer = new FileSender(tox, filenumber, chatNumber, info.Length, filename.Split('\\').Last<string>(), filename);
            var control = convdic[chatNumber].AddNewFileTransfer(tox, transfer);
            transfer.Tag = control;

            control.SetStatus(string.Format("Waiting for {0} to accept...", getFriendName(chatNumber)));
            control.AcceptButton.Visibility = Visibility.Collapsed;
            control.DeclineButton.Visibility = Visibility.Visible;

            control.OnDecline += delegate(FileTransfer ft)
            {
                ft.Kill(false);

                if (transfers.Contains(ft))
                    transfers.Remove(ft);
            };

            control.OnPause += delegate(FileTransfer ft)
            {
                if (ft.Paused)
                    tox.FileSendControl(ft.FriendNumber, 1, ft.FileNumber, ToxFileControl.Pause, new byte[0]);
                else
                    tox.FileSendControl(ft.FriendNumber, 0, ft.FileNumber, ToxFileControl.Accept, new byte[0]);
            };

            transfers.Add(transfer);
            ScrollChatBox();
        }

        private void ExecuteActionsOnNotifyIcon()
        {
            nIcon.Visible = config.HideInTray;
        }

        private void mv_Activated(object sender, EventArgs e)
        {
            nIcon.Icon = notifyIcon;
            ViewModel.HasNewMessage = false;
        }

        private void AccentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var theme = ThemeManager.DetectAppStyle(System.Windows.Application.Current);
            var accent = ThemeManager.GetAccent(((AccentColorMenuData)AccentComboBox.SelectedItem).Name);
            ThemeManager.ChangeAppStyle(System.Windows.Application.Current, accent, theme.Item1);
        }

        private void SettingsFlyout_IsOpenChanged(object sender, RoutedEventArgs e)
        {
            if (!SettingsFlyout.IsOpen && !savingSettings)
            {
                ThemeManager.ChangeAppStyle(System.Windows.Application.Current, oldAccent, oldAppTheme);
            }
            else if (savingSettings)
            {
                savingSettings = false;
            }
        }

        private void AppThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var theme = ThemeManager.DetectAppStyle(System.Windows.Application.Current);
            var appTheme = ThemeManager.GetAppTheme(((AppThemeMenuData)AppThemeComboBox.SelectedItem).Name);
            ThemeManager.ChangeAppStyle(System.Windows.Application.Current, theme.Item2, appTheme);
        }

        private void ExportDataButton_OnClick(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Export Tox data";
            dialog.InitialDirectory = Environment.CurrentDirectory;

            if (dialog.ShowDialog() != true)
                return;

            try { File.WriteAllBytes(dialog.FileName, tox.GetData().Bytes); }
            catch { this.ShowMessageAsync("Error", "Could not export data."); }
        }

        private void AvatarMenuItem_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)e.Source;
            AvatarMenuItem item = (AvatarMenuItem)menuItem.Tag;

            switch (item)
            {
                case AvatarMenuItem.ChangeAvatar:
                    changeAvatar();
                    break;
                case AvatarMenuItem.RemoveAvatar:
                    removeAvatar();
                    break;
            }
        }

        private void changeAvatar()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Image files (*.png, *.gif, *.jpeg, *.jpg) | *.png;*.gif;*.jpeg;*.jpg";
            dialog.InitialDirectory = Environment.CurrentDirectory;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != true)
                return;

            string filename = dialog.FileName;
            FileInfo info = new FileInfo(filename);
          
            byte[] avatarBytes = File.ReadAllBytes(filename);
            MemoryStream stream = new MemoryStream(avatarBytes);
            Bitmap bmp = new Bitmap(stream);

            if(bmp.RawFormat != ImageFormat.Png)
            {
                var memStream = new MemoryStream();
                bmp.Save(memStream, ImageFormat.Png);
                bmp.Dispose();

                bmp = new Bitmap(memStream);
                avatarBytes = memStream.ToArray();
            }
            
            if (avatarBytes.Length > 0x4000)
            {
                double width = 64, height = 64;
                Bitmap newBmp = new Bitmap((int)width, (int)height);

                using (Graphics g = Graphics.FromImage(newBmp))
                {
                    double ratioX = width / (double)bmp.Width;
                    double ratioY = height / (double)bmp.Height;
                    double ratio = ratioX < ratioY ? ratioX : ratioY;

                    int newWidth = (int)(bmp.Width * ratio);
                    int newHeight = (int)(bmp.Height * ratio);

                    int posX = (int)((width - (bmp.Width * ratio)) / 2);
                    int posY = (int)((height - (bmp.Height * ratio)) / 2);
                    
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, posX, posY, newWidth, newHeight);
                }

                bmp.Dispose();

                bmp = newBmp;
                avatarBytes = avatarBitmapToBytes(bmp);

                if (avatarBytes.Length > 0x4000)
                {
                    this.ShowMessageAsync("Error", "This image is bigger than 16 KB and Toxy could not resize the image.");
                    return;
                }
            }

            ViewModel.MainToxyUser.Avatar = bmp.ToBitmapImage(ImageFormat.Png);
            bmp.Dispose();

            if (tox.SetAvatar(ToxAvatarFormat.Png, avatarBytes))
            {
                string avatarsDir = Path.Combine(toxDataDir, "avatars");
                string selfAvatarFile = Path.Combine(avatarsDir, tox.Id.PublicKey.GetString() + ".png");

                if (!Directory.Exists(avatarsDir))
                    Directory.CreateDirectory(avatarsDir);

                File.WriteAllBytes(selfAvatarFile, avatarBytes);
            }

            //let's announce our new avatar
            foreach (int friend in tox.FriendList)
            {
                if (!tox.IsOnline(friend))
                    continue;

                tox.SendAvatarInfo(friend);
            }
        }

        private byte[] avatarBitmapToBytes(Bitmap bmp)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bmp.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private void removeAvatar()
        {
            if (tox.UnsetAvatar())
            {
                ViewModel.MainToxyUser.Avatar = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/profilepicture.png"));

                if (!config.Portable)
                {
                    string path = Path.Combine(toxDataDir, "avatar.png");

                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    if (File.Exists("avatar.png"))
                        File.Delete("avatar.png");
                }
            }
        }

        private void AvatarImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            AvatarContextMenu.PlacementTarget = this;
            AvatarContextMenu.IsOpen = true;
        }

        private void MessageAlertIncrement(IChatObject chat)
        {
            if (!chat.Selected)
            {
                chat.HasNewMessage = true;
                chat.NewMessageCount++;
            }

            if (config.EnableAudioNotifications && call == null)
            {
                if (WindowState == WindowState.Normal && config.AlwaysNotify && !chat.Selected)
                {
                    Win32.Winmm.PlayMessageNotify();
                }
                else if (WindowState == WindowState.Minimized || !IsActive)
                {
                    Win32.Winmm.PlayMessageNotify();
                }
            }
        }

        private void MessageAlertClear(IChatObject chat)
        {
            chat.HasNewMessage = false;
            chat.NewMessageCount = 0;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                foreach (IChatObject chat in this.ViewModel.ChatCollection)
                {
                    if (!chat.Name.ToLower().Contains(SearchBox.Text.ToLower()))
                    {
                        if (chat.GetType() == typeof(FriendControlModelView) || chat.GetType() == typeof(GroupControlModelView))
                        {
                            var view = (BaseChatModelView)chat;
                            view.Visible = false;
                        }
                    }
                }
            }
            else
            {
                foreach (IChatObject chat in this.ViewModel.ChatCollection)
                {
                    if (chat.GetType() == typeof(FriendControlModelView) || chat.GetType() == typeof(GroupControlModelView))
                    {
                        var view = (BaseChatModelView)chat;
                        view.Visible = true;
                    }
                }
            }
        }

        private async void GroupMenuItem_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)e.Source;
            GroupMenuItem item = (GroupMenuItem)menuItem.Tag;

            /*if (item == GroupMenuItem.TextAudio && call != null)
            {
                await this.ShowMessageAsync("Error", "Could not create audio groupchat, there's already a call in progress.");
                return;
            }*/

            if ( item == GroupMenuItem.Create)
            {
                int groupNumber = tox.NewGroup("testing");
                if (groupNumber != -1)
                {
                    AddGroupToView(groupNumber, (ToxGroupType)item);
                }
            }

            if (item == GroupMenuItem.Join)
            {
                /*call = new ToxGroupCall(toxav, groupNumber);
                call.FilterAudio = config.FilterAudio;
                call.Start(config.InputDevice, config.OutputDevice, ToxAv.DefaultCodecSettings);*/
                string input = await this.ShowInputAsync("Join group", "Enter an invitation key to join a group");
                int lel = tox.JoinGroup(input);
            }

            //tox.SetGroupTitle(groupNumber, string.Format("Groupchat #{0}", groupNumber));
        }

        private async void mv_Loaded(object sender, RoutedEventArgs e)
        {
            ToxOptions options;
            if (config.ProxyType != ToxProxyType.None)
                options = new ToxOptions(config.Ipv6Enabled, config.ProxyType, config.ProxyAddress, config.ProxyPort);
            else
                options = new ToxOptions(config.Ipv6Enabled, config.UdpDisabled);

            tox = new Tox(options);
            tox.OnNameChange += tox_OnNameChange;
            tox.OnFriendMessage += tox_OnFriendMessage;
            tox.OnFriendAction += tox_OnFriendAction;
            tox.OnFriendRequest += tox_OnFriendRequest;
            tox.OnUserStatus += tox_OnUserStatus;
            tox.OnStatusMessage += tox_OnStatusMessage;
            tox.OnTypingChange += tox_OnTypingChange;
            tox.OnConnectionStatusChanged += tox_OnConnectionStatusChanged;
            tox.OnFileSendRequest += tox_OnFileSendRequest;
            tox.OnFileData += tox_OnFileData;
            tox.OnFileControl += tox_OnFileControl;
            tox.OnReadReceipt += tox_OnReadReceipt;
            tox.OnConnected += tox_OnConnected;
            tox.OnDisconnected += tox_OnDisconnected;
            tox.OnAvatarData += tox_OnAvatarData;
            tox.OnAvatarInfo += tox_OnAvatarInfo;
            tox.OnGroupTopicChanged += tox_OnGroupTitleChanged;

            tox.OnGroupMessage += tox_OnGroupMessage;
            tox.OnGroupAction += tox_OnGroupAction;
            tox.OnGroupPeerlistUpdate += tox_OnGroupNamelistChange;
            tox.OnGroupNickChanged += tox_OnGroupNickChanged;
            tox.OnGroupSelfJoin += tox_OnGroupSelfJoin;
            tox.OnGroupPeerJoined += tox_OnGroupPeerJoined;
            tox.OnGroupPeerExit += tox_OnGroupPeerExit;
            tox.OnGroupReject += tox_OnGroupReject;
            tox.OnGroupSelfTimeout += tox_OnGroupSelfTimeout;
            tox.OnGroupInvite += tox_OnGroupInvite;

            toxav = new ToxAv(tox.Handle, 1);
            toxav.OnInvite += toxav_OnInvite;
            toxav.OnStart += toxav_OnStart;
            toxav.OnEnd += toxav_OnEnd;
            toxav.OnPeerTimeout += toxav_OnEnd;
            toxav.OnRequestTimeout += toxav_OnEnd;
            toxav.OnReject += toxav_OnEnd;
            toxav.OnCancel += toxav_OnEnd;
            toxav.OnReceivedAudio += toxav_OnReceivedAudio;
            toxav.OnReceivedVideo += toxav_OnReceivedVideo;
            toxav.OnPeerCodecSettingsChanged += toxav_OnPeerCodecSettingsChanged;
            toxav.OnReceivedGroupAudio += toxav_OnReceivedGroupAudio;

            await loadTox();

            if (config.Nodes.Length >= 4)
            {
                var random = new Random();
                var indices = new List<int>();

                for (int i = 0; i < 4; )
                {
                    int index = random.Next(config.Nodes.Length);
                    if (indices.Contains(index))
                        continue;

                    var node = config.Nodes[index];
                    if (bootstrap(config.Nodes[index]))
                    {
                        indices.Add(index);
                        i++;
                    }
                }
            }
            else
            {
                foreach(var node in config.Nodes)
                    bootstrap(node);
            }

            tox.Start();
            toxav.Start();

            if (string.IsNullOrEmpty(getSelfName()))
                tox.Name = "Tox User";

            if (string.IsNullOrEmpty(getSelfStatusMessage()))
                tox.StatusMessage = "Toxing on Toxy";

            ViewModel.MainToxyUser.Name = getSelfName();
            ViewModel.MainToxyUser.StatusMessage = getSelfStatusMessage();

            InitializeNotifyIcon();

            SetStatus(null, false);
            InitFriends();

            TextToSend.AddHandler(DragOverEvent, new DragEventHandler(Chat_DragOver), true);
            TextToSend.AddHandler(DropEvent, new DragEventHandler(Chat_Drop), true);

            ChatBox.AddHandler(DragOverEvent, new DragEventHandler(Chat_DragOver), true);
            ChatBox.AddHandler(DropEvent, new DragEventHandler(Chat_Drop), true);

            if (tox.FriendCount > 0)
                ViewModel.SelectedChatObject = ViewModel.ChatCollection.OfType<IFriendObject>().FirstOrDefault();

            initDatabase();
            loadAvatars();
        }

        private void tox_OnGroupSelfTimeout(object sender, ToxEventArgs.GroupSelfTimeoutEventArgs e)
        {
            Debug.WriteLine(string.Format("Groupchat {0}: we timed out", e.GroupNumber));
        }

        private void tox_OnGroupInvite(object sender, ToxEventArgs.GroupInviteEventArgs e)
        {
            Debug.WriteLine(string.Format("Received invite to groupchat, accepting"));

            Dispatcher.BeginInvoke(((Action)(() =>
            {
                int number = tox.AcceptInvite(e.Data);
                var group = ViewModel.GetGroupObjectByNumber(number);

                if (group != null)
                    SelectGroupControl(group);
                else if (number != -1)
                    AddGroupToView(number, e.GroupType);
            })));
        }

        private void tox_OnGroupReject(object sender, ToxEventArgs.GroupRejectEventArgs e)
        {
            Debug.WriteLine(string.Format("Join request rejected: {0}", e.Reason));
        }

        private void tox_OnGroupPeerExit(object sender, ToxEventArgs.GroupPeerExitEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group == null)
                    return;

                RearrangeGroupPeerList(group);

                var data = new MessageData() { Username = "*  ", Message = string.Format("{0} has left the chat ({1})", tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber), e.PartMessage), IsAction = true, Timestamp = DateTime.Now };
                AddMessageToView(e.GroupNumber, data, true);

                ScrollChatBox();
            })));
        }

        private void tox_OnGroupPeerJoined(object sender, ToxEventArgs.GroupPeerJoinedEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group == null)
                    return;

                RearrangeGroupPeerList(group);

                var data = new MessageData() { Username = "*  ", Message = string.Format("{0} has joined the chat", tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber)), IsAction = true, Timestamp = DateTime.Now };
                AddMessageToView(e.GroupNumber, data, true);

                ScrollChatBox();
            })));
        }

        private void tox_OnGroupSelfJoin(object sender, ToxEventArgs.GroupSelfJoinEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = AddGroupToView(e.GroupNumber, ToxGroupType.Text);
                RearrangeGroupPeerList(group);

                group.Name = tox.GetGroupTopic(e.GroupNumber);
            })));
        }

        private void tox_OnGroupNickChanged(object sender, ToxEventArgs.GroupNickChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(((Action)(() =>
            {
                var group = ViewModel.GetGroupObjectByNumber(e.GroupNumber);
                if (group == null)
                    return;

                var peer = group.PeerList.GetPeerByPeerNumber(e.PeerNumber);
                if (peer != null)
                {
                    var data = new MessageData() { Username = "*  ", Message = string.Format("{0} is now known as {1}", peer.Name, tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber)), IsAction = true, Timestamp = DateTime.Now };
                    AddMessageToView(e.GroupNumber, data, true);

                    peer.Name = tox.GetGroupMemberName(e.GroupNumber, e.PeerNumber);
                    RearrangeGroupPeerList(group);
                }
            })));
        }

        private bool bootstrap(ToxConfigNode node)
        {
            bool success = tox.BootstrapFromNode(new ToxNode(node.Address, node.Port, new ToxKey(ToxKeyType.Public, node.ClientId)));
            if (success)
                Debug.WriteLine("Bootstrapped off of {0}:{1}", node.Address, node.Port);
            else
                Debug.WriteLine("Could not bootstrap off of {0}:{1}", node.Address, node.Port);

            return success;
        }

        private string[] GetProfileNames(string path)
        {
            if (!Directory.Exists(path))
                return null;

            List<string> profiles = new List<string>();

            foreach (string profile in Directory.GetFiles(path, "*.tox", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".tox")))
                profiles.Add(profile.Substring(0, profile.LastIndexOf(".tox")).Split('\\').Last());

            return profiles.ToArray();
        }

        private async void SwitchProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string[] profiles = GetProfileNames(toxDataDir);
            if (profiles == null && profiles.Length < 1)
                return;

            var dialog = new SwitchProfileDialog(profiles, this);
            await this.ShowMetroDialogAsync(dialog);
            var result = await dialog.WaitForButtonPressAsync();
            await this.HideMetroDialogAsync(dialog);

            if (result == null)
                return;

            if (result.Result == SwitchProfileDialogResult.OK)
            {
                if (string.IsNullOrEmpty(result.Input))
                    return;

                if (!LoadProfile(result.Input, false))
                    await this.ShowMessageAsync("Error", "Could not load profile, make sure it exists/is accessible.");
            }
            else if (result.Result == SwitchProfileDialogResult.New)
            {
                string profile = await this.ShowInputAsync("New Profile", "Enter a name for your new profile.");
                if (string.IsNullOrEmpty(profile))
                    await this.ShowMessageAsync("Error", "Could not create profile, you must enter a name for your profile.");
                else
                {
                    if (!CreateNewProfile(profile))
                        await this.ShowMessageAsync("Error", "Could not create profile, did you enter a valid name?");
                }
            }
            else if (result.Result == SwitchProfileDialogResult.Import)
            {
                ToxData data = ToxData.FromDisk(result.Input);
                Tox t = new Tox(new ToxOptions());

                if (data == null || !t.Load(data))
                {
                    await this.ShowInputAsync("Error", "Could not load tox profile.");
                }
                else
                {
                    string profile = await this.ShowInputAsync("New Profile", "Enter a name for your new profile.");
                    if (string.IsNullOrEmpty(profile))
                        await this.ShowMessageAsync("Error", "Could not create profile, you must enter a name for your profile.");
                    else
                    {
                        string path = Path.Combine(toxDataDir, profile + ".tox");
                        if (!File.Exists(path))
                        {
                            t.Name = profile;

                            if (t.GetData().Save(path))
                                if (!LoadProfile(profile, false))
                                    await this.ShowMessageAsync("Error", "Could not load profile, make sure it exists/is accessible.");
                        }
                        else
                        {
                            await this.ShowMessageAsync("Error", "Could not create profile, a profile with the same name already exists.");
                        }
                    }
                }
            }
        }

        private bool CreateNewProfile(string profileName)
        {
            string path = Path.Combine(toxDataDir, profileName + ".tox");
            if (File.Exists(path))
                return false;

            Tox t = new Tox(new ToxOptions());
            t.Name = profileName;

            if (!t.GetData().Save(path))
            {
                t.Dispose();
                return false;
            }

            t.Dispose();
            return LoadProfile(profileName, false);
        }

        private bool LoadProfile(string profile, bool allowReload)
        {
            if (config.ProfileName == profile && !allowReload)
                return true;

            if (!File.Exists(Path.Combine(toxDataDir, profile + ".tox")))
                return false;

            KillTox(false);
            ViewModel.ChatCollection.Clear();

            config.ProfileName = profile;
            mv_Loaded(this, new RoutedEventArgs());

            return true;
        }

        public void GroupPeerCopyKey_Click(object sender, RoutedEventArgs e)
        {
            /*var peer = GroupListView.SelectedItem as GroupPeer;
            if (peer == null)
                return;

            Clipboard.Clear();
            Clipboard.SetText(peer.PublicKey.GetString());*/
        }

        private void GroupPeerMute_Click(object sender, RoutedEventArgs e)
        {
            var peer = GroupListView.SelectedItem as GroupPeer;
            if (peer == null)
                return;

            peer.Muted = !peer.Muted;
        }

        private void GroupPeerIgnore_Click(object sender, RoutedEventArgs e)
        {
            var peer = GroupListView.SelectedItem as GroupPeer;
            if (peer == null)
                return;

            peer.Ignored = !peer.Ignored;
        }

        private void MicButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (call == null || call.GetType() != typeof(ToxGroupCall))
                return;

            var groupCall = (ToxGroupCall)call;
            groupCall.Muted = !groupCall.Muted;
        }

        private void VideoButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (call == null || call.GetType() == typeof(ToxGroupCall))
                return;

            call.ToggleVideo((bool)VideoButton.IsChecked, config.VideoDevice);
        }

        private void ProcessVideoFrame(IntPtr frame)
        {
            var vpxImage = VpxImage.FromPointer(frame);
            byte[] dest = VpxHelper.Yuv420ToRgb(vpxImage, vpxImage.d_w * vpxImage.d_h * 4);

            vpxImage.Free();

            int bytesPerPixel = (PixelFormats.Bgra32.BitsPerPixel + 7) / 8;
            int stride = 4 * (((int)vpxImage.d_w * bytesPerPixel + 3) / 4);

            var source = BitmapSource.Create((int)vpxImage.d_w, (int)vpxImage.d_h, 96d, 96d, PixelFormats.Bgra32, null, dest, stride);
            source.Freeze();

            Dispatcher.Invoke(() => VideoChatImage.Source = source);
        }

        private void NameTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            OpenSettings_Click(null, null);

            RoutedEventHandler handler = null;
            handler = (s, args) =>
            {
                if (SettingsFlyout.IsOpen)
                {
                    Keyboard.Focus(SettingsUsername);
                    SettingsUsername.SelectAll();
                    SettingsFlyout.IsOpenChanged -= handler;
                }
            };

            SettingsFlyout.IsOpenChanged += handler;
        }

        private void StatusTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            OpenSettings_Click(null, null);

            RoutedEventHandler handler = null;
            handler = (s, args) =>
            {
                if (SettingsFlyout.IsOpen)
                {
                    Keyboard.Focus(SettingsStatus);
                    SettingsStatus.SelectAll();
                    SettingsFlyout.IsOpenChanged -= handler;
                }
            };

            SettingsFlyout.IsOpenChanged += handler;
        }

        private void RandomNospamButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] buffer = new byte[sizeof(uint)];
            var random = new Random();

            random.NextBytes(buffer);
            SettingsNospam.Text = BitConverter.ToUInt32(buffer, 0).ToString();
        }
    }
}
