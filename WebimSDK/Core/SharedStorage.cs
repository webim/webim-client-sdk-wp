using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebimSDK
{
    using WebimSDK.Extensions;

    class SharedStorage
    {
        #region Singleton

        private static volatile SharedStorage instance;
        private static object syncRoot = new Object();

        private SharedStorage() { }

        public static SharedStorage Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new SharedStorage();
                    }
                }

                return instance;
            }
        }

        #endregion

        #region Storage accessors

        private bool hasPendingChanges;

        private string SetWatchedProperty(string gotValue, string newValue)
        {
            if (!StringExtensions.ExistsAndEquals(gotValue, newValue))
            {
                hasPendingChanges = true;
                return newValue;
            }
            return gotValue;
        }

        public string Account
        {
            get
            {
                return _Storage.Account;
            }
            set
            {
                _Storage.Account = SetWatchedProperty(_Storage.Account, value);
            }
        }

        public string Location
        {
            get
            {
                return _Storage.Location;
            }
            set
            {
                _Storage.Location = SetWatchedProperty(_Storage.Location, value);
            }
        }

        public string Platform
        {
            get
            {
                return _Storage.Platform;
            }
            set
            {
                _Storage.Platform = SetWatchedProperty(_Storage.Platform, value);
            }
        }

        public string Token
        {
            get
            {
                return _Storage.Token;
            }
            set
            {
                _Storage.Token = SetWatchedProperty(_Storage.Token, value);
            }
        }

        public string Visitor
        {
            get
            {
                return _Storage.Visitor;
            }
            set
            {
                _Storage.Visitor = SetWatchedProperty(_Storage.Token, value);
            }
        }

        public string VisitorID
        {
            get
            {
                return _Storage.VisitorID;
            }
            set
            {
                _Storage.VisitorID = SetWatchedProperty(_Storage.VisitorID, value);
            }
        }

        public string VisitSessionID
        {
            get
            {
                return _Storage.VisitSessionID;
            }
            set
            {
                _Storage.VisitSessionID = SetWatchedProperty(_Storage.VisitSessionID, value);
            }
        }

        public string PageID
        {
            get
            {
                return _Storage.PageID;
            }
            set
            {
                _Storage.PageID = SetWatchedProperty(_Storage.PageID, value);
            }
        }

        public List<WMChat> ChatsList
        {
            get
            {
                return _Storage.ChatsList;
            }
            set
            {
                hasPendingChanges = true;
                _Storage.ChatsList = value;
            }
        }

        public double LastChangeTs
        {
            get
            {
                return _Storage.LastChangeTs;
            }
            set
            {
                if (_Storage.LastChangeTs != value)
                {
                    hasPendingChanges = true;
                    _Storage.LastChangeTs = value;
                }
            }
        }

        #endregion

        // Realtime section

        public double Revision { get; set; }
        public string VisitorFields { get; set; }

        // Privary usage

        internal delegate void SharedStorageClearedHandler();
        public event SharedStorageClearedHandler SharedStorageCleared;

        // Class is usabe only after it has been initialized. See this property before access to properties
        internal bool isInitialized { get; private set; }

        private Storage _Storage = null;

        internal async Task InitializeForAccount(string account)
        {
            if (isInitialized)
            {
                return;
            }
            _Storage = await Storage.LoadFromFile();
            if (string.IsNullOrEmpty(_Storage.Account))
            {
                _Storage.Account = account;
            }
            else if (!_Storage.Account.Equals(account))
            {
                await _Storage.ClearStorageFile();
                _Storage.ClearMemoryData();
                _Storage.Account = account;
                if (SharedStorageCleared != null)
                {
                    SharedStorageCleared();
                }
            }
            else
            {
                isInitialized = true;
                hasPendingChanges = true;
                return;
            }
            await _Storage.SaveToFile();
            isInitialized = true;
            hasPendingChanges = false;
        }

        internal async Task ClearStorageRecoverableData()
        {
            await _Storage.ClearStorageFile();
            _Storage.ClearMemoryData();
            hasPendingChanges = true;
        }

        internal async Task SaveToFile()
        {
            await _Storage.SaveToFile();
            hasPendingChanges = false;
        }

        internal async Task Flush()
        {
            if (hasPendingChanges)
            {
                await SaveToFile();
            }
        }

        internal void OfflineAddChat(WMChat chat)
        {
            hasPendingChanges = true;
            _Storage.ChatsList.Add(chat);
        }

        internal bool OfflineRemoveChat(WMChat chat)
        {
            hasPendingChanges = true;
            return _Storage.ChatsList.Remove(chat);
        }

        public WMChat FindChatByID(string chatID)
        {
            return _Storage.FindChatByID(chatID);
        }
    }
}
