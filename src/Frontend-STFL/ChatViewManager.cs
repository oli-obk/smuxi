/*
 * Smuxi - Smart MUltipleXed Irc
 *
 * Copyright (c) 2007, 2010-2013 Mirco Bauer <meebey@meebey.net>
 *
 * Full GPL License: <http://www.gnu.org/licenses/gpl.txt>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307 USA
 */

using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using Mono.Unix;
using Stfl;
using Smuxi.Common;
using Smuxi.Engine;
using Smuxi.Frontend;

namespace Smuxi.Frontend.Stfl
{
    public class ChatViewManager : ChatViewManagerBase<ChatView>
    {
#if LOG4NET
        private static readonly log4net.ILog _Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
#endif
        private MainWindow                      f_MainWindow;
        private ChatView                        f_CurrentChat;
        private Dictionary<ChatModel, ChatView> f_ChatViews = new Dictionary<ChatModel, ChatView>();
        private List<ChatView>                  f_ChatViewList = new List<ChatView>();

        [Obsolete("This property is deprecated, use ActiveChat instead")]
        public ChatView CurrentChat {
            get {
                return ActiveChat;
            }
            set {
                ActiveChat = value;
            }
        }

        public override ChatView ActiveChat {
            get {
                return f_CurrentChat;
            }
            set {
                ChatView oldchat = ActiveChat;
                if (f_CurrentChat != null) {
                    f_CurrentChat.IsVisible = false;
                }

                f_CurrentChat = value;
                if (f_CurrentChat != null) {
#if LOG4NET
                    _Logger.Debug("set_CurrentChat(): making " + value.ChatModel.ID + " visible");
#endif
                    f_CurrentChat.IsVisible = true;
                    UpdateNavigation();
                    UpdateInput();
                    UpdateTitle();
                    UpdateTopic();
                }

                OnChatSwitched(new ChatViewManagerChatSwitchedEventArgs<ChatView>(oldchat, value));
            }
        }

        public int CurrentChatNumber {
            get {
                if (ActiveChat == null) {
                    return -1;
                }
                return f_ChatViewList.IndexOf(ActiveChat);
            }
            set {
                if (value < 0 || value >= f_ChatViewList.Count) {
                    return;
                }

                ActiveChat = f_ChatViewList[value];
            }
        }

        public ChatViewManager(MainWindow mainWindow)
        {
           if (mainWindow == null) {
                throw new ArgumentNullException("mainWindow");
           }

           f_MainWindow = mainWindow;
        }
        
        public override void AddChat(ChatModel chat)
        {
            ChatView chatView = (ChatView) CreateChatView(chat, f_MainWindow);
            f_ChatViews.Add(chat, chatView);
            f_ChatViewList.Add(chatView);
            OnChatAdded(new ChatViewManagerChatAddedEventArgs<ChatView>(chatView));

            if (CurrentChat == null) {
                CurrentChat = chatView;
            }

            UpdateNavigation();
        }

        public override void RemoveChat(ChatModel chat)
        {
            var chatView = GetChat(chat);
            chatView.IsVisible = false;

            if (CurrentChat == chatView) {
                CurrentChatNumber--;
            }

            f_ChatViews.Remove(chat);
            f_ChatViewList.Remove(chatView);

            OnChatRemoved(new ChatViewManagerChatRemovedEventArgs<ChatView>(chatView));
            chatView.Dispose();

            UpdateNavigation();
        }
        
        public override void EnableChat(ChatModel chat)
        {
           ChatView chatView = f_ChatViews[chat];
           chatView.Enable();
        }
        
        public override void DisableChat(ChatModel chat)
        {
           ChatView chatView = f_ChatViews[chat];
           chatView.Disable();
        }
        
        public ChatView GetChat(ChatModel chat)
        {
            return f_ChatViews[chat];
        }
        
        public ChatView GetChat(int chat)
        {
            if (chat < 0 || chat >= f_ChatViewList.Count) {
                return null;
            }
            return f_ChatViewList[chat];
        }

        public void UpdateNavigation()
        {
            var nav = new StringBuilder();
            foreach (var chat in f_ChatViewList) {
                nav.AppendFormat("[{0}] ",
                                 chat == CurrentChat ? chat.Name : chat.Label);
            }
            if (nav.Length > 0) {
                nav.Length--;
            }

            f_MainWindow.NavigationLabel = nav.ToString();
        }

        public void UpdateInput()
        {
            var chatView = CurrentChat;
            if (chatView == null) {
                return;
            }

            f_MainWindow.InputLabel = String.Format("[{0}]", chatView.Name);
        }

        void UpdateTitle()
        {
            var chatView = CurrentChat;
            if (chatView == null) {
                return;
            }

            string title;
            var chatModel = chatView.ChatModel;
            string protocolStatus = null;
            if (chatModel.ProtocolManager != null) {
                protocolStatus = chatModel.ProtocolManager.ToString();
            }
            if (chatModel is SessionChatModel) {
                title = String.Empty;
            } else if (chatModel is ProtocolChatModel) {
                title = protocolStatus;
            } else {
                title = String.Format("{0} @ {1}",
                                      chatModel.Name,
                                      protocolStatus);
            }
            if (!String.IsNullOrEmpty(title)) {
                title += " - ";
            }
            title += "Smuxi";

            f_MainWindow.TitleLabel = title;
            // HACK: set xterm window title
            if (StflApi.IsXterm) {
                NcursesApi.endwin();
                Console.WriteLine((char) 27 + "]0;{0}" + (char) 7, title);
                NcursesApi.refresh();
            }
        }

        public void UpdateTopic()
        {
            string topic = String.Empty;
            var chatView = CurrentChat;

            if (chatView is GroupChatView) {
                var topicModel = ((GroupChatView) chatView).Topic;

                if (topicModel != null) {
                    topic = topicModel.ToString();
                }
            }

            f_MainWindow.TopicLabel = topic;
            f_MainWindow.ShowTopic = !String.IsNullOrEmpty(topic);
        }
    }
}
