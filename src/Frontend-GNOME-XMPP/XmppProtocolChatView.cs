// Smuxi - Smart MUltipleXed Irc
// 
// Copyright (c) 2013 oliver
// 
// Full GPL License: <http://www.gnu.org/licenses/gpl.txt>
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307 USA
using System;
using Smuxi.Common;
using Smuxi.Engine;
using System.Threading;
using agsXMPP;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Smuxi.Frontend.Gnome
{
    [Serializable]
    public class SerializationTest : ISerializable
    {
        private XmppProtocolChatView View { get; set; }
        
        public SerializationTest(XmppProtocolChatView view)
        {
            View = view;
            var xmpp = View.XmppProtocolManager;
            xmpp.OnContactChanged += OnContactChanged;
        }
        
        void OnContactChanged(XmppPersonModel person)
        {
            ThreadPool.QueueUserWorkItem(delegate {
                try {
                    View.OnContactChanged(person);
                } catch (Exception ex) {
                    Frontend.ShowException(ex);
                }
            });
        }
        
        protected SerializationTest(SerializationInfo info, StreamingContext ctx)
        {
            if (info == null) {
                throw new ArgumentNullException("info");
            }
        }
        
        public void GetObjectData(SerializationInfo info, StreamingContext ctx)
        {
            if (info == null) {
                throw new ArgumentNullException("info");
            }
        }
    }
    
    [ChatViewInfo(ChatType = ChatType.Protocol, ProtocolManagerType = typeof(XmppProtocolManager))]
    public class XmppProtocolChatView : ProtocolChatView
    {
#if LOG4NET
        private static readonly log4net.ILog _Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
#endif
        private static readonly string _LibraryTextDomain = "smuxi-frontend-gnome-xmpp";
        public XmppProtocolManager XmppProtocolManager { get; set; }
        Gtk.VPaned OutputSplit { get; set; }
        Gtk.HBox ContactListSplit { get; set; }
        
        internal class Group
        {
            public Gtk.TreeView view { get; set; }
            public Gtk.ListStore store { get; set; }
            public Gtk.ScrolledWindow win { get; set; }
            public Gtk.TreeViewColumn column { get; set; }
            
            int SortPersonListStore(Gtk.TreeModel model,
                                                      Gtk.TreeIter iter1,
                                                      Gtk.TreeIter iter2)
            {
                Gtk.ListStore liststore = (Gtk.ListStore) model;
                
                PersonModel person1 = (XmppPersonModel) liststore.GetValue(iter1, 0); 
                PersonModel person2 = (XmppPersonModel) liststore.GetValue(iter2, 0); 
    
                return person1.CompareTo(person2);
            }
            
            void RenderPersonIdentityName(Gtk.TreeViewColumn column,
                                                     Gtk.CellRenderer cellr,
                                                     Gtk.TreeModel model, Gtk.TreeIter iter)
            {
                XmppPersonModel person = (XmppPersonModel) model.GetValue(iter, 0);
                var renderer = (Gtk.CellRendererText) cellr;
                //if (NickColors) {
                if (true) {
                    // TODO: do we need to optimize this? it's called very often...
                    Gdk.Color bgColor = column.TreeView.Style.Base(Gtk.StateType.Normal);
                    var builder = new MessageBuilder();
                    builder.NickColors = true;
                    builder.AppendNick(person);
                    renderer.Markup = PangoTools.ToMarkup(builder.ToMessage(),
                                                          bgColor);
                } else {
                    renderer.Text = person.IdentityName;
                }
            }
            
            void OnPersonTreeViewFocusOutEvent(object sender, EventArgs e)
            {
                Trace.Call(sender, e);
    
                // clear the selection when we loose the focus
                (sender as Gtk.TreeView).Selection.UnselectAll();
            }
            
            public Group(Gtk.RowActivatedHandler handler)
            {
                win = new Gtk.ScrolledWindow();
                win.HscrollbarPolicy = Gtk.PolicyType.Never;
                
                view = new Gtk.TreeView();
                view.BorderWidth = 0;
                view.Selection.Mode = Gtk.SelectionMode.Multiple;
                win.Add(view);
                
                Gtk.CellRendererText cellr = new Gtk.CellRendererText();
                cellr.WidthChars = 12;
                column = new Gtk.TreeViewColumn(String.Empty, cellr);
                column.SortColumnId = 0;
                column.Spacing = 0;
                column.SortIndicator = false;
                column.Sizing = Gtk.TreeViewColumnSizing.Autosize;
                // FIXME: this callback leaks memory
                column.SetCellDataFunc(cellr, new Gtk.TreeCellDataFunc(RenderPersonIdentityName));
                view.AppendColumn(column);
                
                store = new Gtk.ListStore(typeof(XmppPersonModel));
                store.SetSortColumnId(0, Gtk.SortType.Ascending);
                store.SetSortFunc(0, new Gtk.TreeIterCompareFunc(SortPersonListStore));
                
                view.Model = store;
                view.RowActivated += handler;
                view.FocusOutEvent += OnPersonTreeViewFocusOutEvent;
            }
            
            private bool TryGetIter(Jid jid, out Gtk.TreeIter iter)
            {
                bool res = store.GetIterFirst(out iter);
                if (!res) {
                    return false;
                }
                
                do {
                    XmppPersonModel person = (XmppPersonModel) store.GetValue(iter, 0);
                    if (person.Jid == jid) {
                        return true;
                    }
                } while (store.IterNext(ref iter));
                return false;
            }

            public void AddOrUpdate(XmppPersonModel p)
            {
                if (p == null) {
#if LOG4NET
                _Logger.Warn("AddOrUpdate() got a null XmppPersonModel");
#endif
                    return;
                }
                Gtk.TreeIter iter;
                if (!TryGetIter(p.Jid, out iter)) {
                    store.AppendValues(p);
                    return;
                }
                store.SetValue(iter, 0, p);
            }

            public void Remove(Jid jid)
            {
                Gtk.TreeIter iter;
                if (TryGetIter(jid, out iter)) {
                    store.Remove(ref iter);
                }
            }
        };
        
        Dictionary<string, Group> Groups { get; set; }
        SerializationTest Test { get; set; }

        public XmppProtocolChatView(ProtocolChatModel chat) : base(chat)
        {
            if (Frontend.EngineVersion < new Version(0,8,11)) {
                return;
            }
            Trace.Call(chat);
            Groups = new Dictionary<string, Group>();
            
            Remove(OutputScrolledWindow);
            ContactListSplit = new Gtk.HBox();
            ContactListSplit.SetSizeRequest(0, 200);
            CreateGroupWidget("offline");
            
            OutputSplit = new Gtk.VPaned();
            OutputSplit.Add1(ContactListSplit);
            
            OutputScrolledWindow.SetSizeRequest(0, 100);
            OutputSplit.Add2(OutputScrolledWindow);
            Add(OutputSplit);
            ShowAll();
        }
        
        Group CreateGroupWidget(string @group)
        {
            var g = new Group(new Gtk.RowActivatedHandler(OnPersonsRowActivated));
            Groups.Add(@group, g);
            var vbox = new Gtk.VBox();
            var lab = new Gtk.Label(@group);
            vbox.PackStart(lab, false, false, 0);
            vbox.Add(g.win);
            ContactListSplit.Add(vbox);
            return g;
        }
        
        Group GetOrCreateGroup(string @group)
        {
            Group g;
            if (!Groups.TryGetValue(@group, out g)) {
                ContactListSplit.PackStart(new Gtk.VSeparator(), false, false, 0);
                g = CreateGroupWidget(@group);
                ShowAll();
            }
            return g;
        }

        private static string _(string msg)
        {
            return LibraryCatalog.GetString(msg, _LibraryTextDomain);
        }

        public override void Sync()
        {
            Trace.Call();

            base.Sync();
            if (Frontend.EngineVersion < new Version(0,8,11)) {
                return;
            }

            XmppProtocolManager = (XmppProtocolManager) ProtocolManager;
            Test = new SerializationTest(this);
        }

        public void OnContactChanged(XmppPersonModel person)
        {
            Group offline = GetOrCreateGroup("offline");
            Group online = GetOrCreateGroup("online");
            if (person.GetResourcesWithHighestPriority().Count == 0) {
                Gtk.Application.Invoke(delegate {
                    offline.AddOrUpdate(person);
                    online.Remove(person.Jid);
                });
            } else {
                Gtk.Application.Invoke(delegate {
                    online.AddOrUpdate(person);
                    offline.Remove(person.Jid);
                });
            }
        }
        
        void OnPersonsRowActivated(object sender, Gtk.RowActivatedArgs e)
        {
            Trace.Call(sender, e);
            Gtk.TreeView tree = sender as Gtk.TreeView;
            Gtk.TreeIter iter;
            if (!tree.Model.GetIter(out iter, e.Path)) {
                // wat?
#if LOG4NET
                _Logger.Warn("wat??");
#endif
                return;
            }
     
            var person = tree.Model.GetValue(iter, 0) as XmppPersonModel;

            var protocolManager = ProtocolManager;
            if (protocolManager == null) {
#if LOG4NET
                _Logger.WarnFormat(
                    "{0}.OnPersonsRowActivated(): ProtocolManager is null, " +
                    "bailing out!", this
                );
#endif
                return;
            }

            // jump to person chat if available
            foreach (var chatView in Frontend.MainWindow.ChatViewManager.Chats) {
                if (!(chatView is PersonChatView)) {
                    continue;
                }
                var personChatView = (PersonChatView) chatView;
                if (personChatView.PersonModel == person) {
                    Frontend.MainWindow.ChatViewManager.CurrentChatView = personChatView;
                    return;
                }
            }

            // this is a generic implemention that should be able to open/create
            // a private chat in most cases, as it depends what OpenChat()
            // of the specific protocol actually expects/needs
            PersonChatModel personChat = new PersonChatModel(
                person,
                person.ID,
                person.IdentityName,
                null
            );

            ThreadPool.QueueUserWorkItem(delegate {
                try {
                    protocolManager.OpenChat(
                        Frontend.FrontendManager,
                        personChat
                    );
                } catch (Exception ex) {
                    Frontend.ShowException(ex);
                }
            });
        }
    }
}

