/*
 * Copyright (c) InWorldz Halcyon Developers
 * Copyright (c) Contributors, http://opensimulator.org/
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;
using OpenMetaverse;
using OpenSim.Data;

namespace OpenSim.Framework.Communications.Cache
{
    internal delegate void AddItemDelegate(InventoryItemBase itemInfo);
    internal delegate void UpdateItemDelegate(InventoryItemBase itemInfo);
    internal delegate void DeleteItemDelegate(UUID itemID);
    internal delegate void QueryItemDelegate(UUID itemID);
    internal delegate void QueryFolderDelegate(UUID folderID);

    internal delegate void CreateFolderDelegate(string folderName, UUID folderID, ushort folderType, UUID parentID);
    internal delegate void MoveFolderDelegate(UUID folderID, UUID parentID);
    internal delegate void PurgeFolderDelegate(UUID folderID);
    internal delegate void UpdateFolderDelegate(string name, UUID folderID, ushort type, UUID parentID);

    internal delegate void SendInventoryDescendentsDelegate(
        IClientAPI client, UUID folderID, bool fetchFolders, bool fetchItems);

    public delegate void OnItemReceivedDelegate(UUID itemID);

    /// <summary>
    /// Stores user profile and inventory data received from backend services for a particular user.
    /// </summary>
    public class CachedUserInfo
    {
        public event OnItemReceivedDelegate OnItemReceived;

        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The comms manager holds references to services (user, grid, inventory, etc.)
        /// </summary>
        private readonly CommunicationsManager m_commsManager;

        public UserProfileData UserProfile { get { return m_userProfile; } }
        private UserProfileData m_userProfile;

        /// <summary>
        /// Have we received the user's inventory from the inventory service?
        /// </summary>
        public bool HasReceivedInventory { get { return true; } }

        /// <summary>
        /// Holds the most appropriate folders for the given type.
        /// Note:  Thus far in the code, this folder doesn't have to be kept up to date as it will 
        /// only be used to retrieve an ID.  If this ever changes, this collection will have to be kept up to date
        /// </summary>
        private Dictionary<int, InventoryFolderBase> _folderTypeCache = new Dictionary<int, InventoryFolderBase>();

        public UUID SessionID
        {
            get { return m_session_id; }
            set { m_session_id = value; }
        }
        private UUID m_session_id = UUID.Zero;

        /// <summary>
        /// List of friends for this user
        /// </summary>
        private List<FriendListItem> _friends;

        /// <summary>
        /// Stores the permissions that were given to me by friends
        /// </summary>
        private Dictionary<UUID, uint> _permissionsGivenByFriends = new Dictionary<UUID, uint>();


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commsManager"></param>
        /// <param name="userProfile"></param>
        public CachedUserInfo(CommunicationsManager commsManager, UserProfileData userProfile, List<FriendListItem> friendInfos)
        {
            m_commsManager = commsManager;
            m_userProfile = userProfile;
            _friends = friendInfos;
            this.IndexFriendPermissions();
        }

        private void FetchFriends(bool force)
        {
            if (m_userProfile == null) return;    // no current user
            if (force || (_friends == null))
            {
                _friends = m_commsManager.UserService.GetUserFriendList(m_userProfile.ID);
                this.IndexFriendPermissions();
            }
        }

        public bool HasPermissionFromFriend(UUID friendId, uint permissionMask)
        {
            FetchFriends(false);
            lock (_permissionsGivenByFriends)
            {
                uint permsGiven;
                if (_permissionsGivenByFriends.TryGetValue(friendId, out permsGiven))
                {
                    if ((permsGiven & permissionMask) != 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        private void IndexFriendPermissions()
        {
            lock (_permissionsGivenByFriends)
            {
                _permissionsGivenByFriends.Clear();

                if (_friends == null)
                    return;

                //index user permissions given by each friend
                foreach (FriendListItem friendItem in _friends)
                {
                    _permissionsGivenByFriends.Add(friendItem.Friend, friendItem.FriendListOwnerPerms);
                }
            }
        }

        public void AdjustPermissionsFromFriend(UUID friendId, uint newPermissions)
        {
            lock (_permissionsGivenByFriends)
            {
                _permissionsGivenByFriends[friendId] = newPermissions;
            }
        }

        public InventoryFolderBase GetFolderAttributes(UUID folderId)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            InventoryFolderBase folderInfo = inventorySelect.GetProvider(m_userProfile.ID).GetFolderAttributes(folderId);

            return folderInfo;
        }

        public InventoryFolderBase GetFolderAttributesChecked(UUID folderId)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            InventoryFolderBase folderInfo = inventorySelect.GetCheckedProvider(m_userProfile.ID).GetFolderAttributes(m_userProfile.ID, folderId);

            return folderInfo;
        }

        public InventoryItemBase FindItem(UUID itemId)
        {
            try
            {
                IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
                InventoryItemBase itemInfo = inventorySelect.GetCheckedProvider(m_userProfile.ID).GetItem(m_userProfile.ID, itemId, UUID.Zero);

                return itemInfo;
            }
            catch (InventoryStorageException e)
            {
                m_log.ErrorFormat("[INVENTORY] Could not find requested item {0}: {1}", itemId, e);
            }

            return null;
        }

        /// <summary>
        /// Fetch inventory for this user.
        /// </summary>
        /// This has to be executed as a separate step once user information is retreived.  
        /// This will occur synchronously if the inventory service is in the same process as this class, and
        /// asynchronously otherwise.
        public void FetchInventory()
        {
        }

        /// <summary>
        /// Flushes the folder type cache
        /// </summary>
        public void DropInventory()
        {
            lock (_folderTypeCache)
            {
                _folderTypeCache.Clear();
            }
        }

        /// <summary>
        /// Callback invoked when an item is received from an async request to the inventory service.
        ///
        /// We're assuming here that items are always received after all the folders
        /// received.
        /// If folder is null, we will search for it starting from RootFolder (an O(n) operation),
        /// otherwise we'll just put it into folder
        /// </summary>
        /// <param name="folderInfo"></param>
        private void ItemReceive(InventoryItemBase itemInfo, InventoryFolderImpl folder)
        {
            if (OnItemReceived != null)
                OnItemReceived(itemInfo.ID);
        }

        /// <summary>
        /// Create a folder in this agent's inventory.
        /// </summary>
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        /// 
        /// <param name="parentID"></param>
        /// <returns></returns>
        public InventoryFolderBase CreateFolder(string folderName, UUID folderID, short folderType, UUID parentID)
        {
            InventoryFolderBase createdBaseFolder = new InventoryFolderBase();
            createdBaseFolder.Owner = UserProfile.ID;
            createdBaseFolder.ID = folderID;
            createdBaseFolder.Name = folderName;
            createdBaseFolder.ParentID = parentID;
            createdBaseFolder.Type = (short)folderType;
            createdBaseFolder.Version = 1;

            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            inventorySelect.GetCheckedProvider(m_userProfile.ID).CreateFolder(UserProfile.ID, createdBaseFolder);

            return createdBaseFolder;
        }

        /// <summary>
        /// Handle a client request to update the inventory folder
        /// </summary>
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        ///
        /// FIXME: We call add new inventory folder because in the data layer, we happen to use an SQL REPLACE
        /// so this will work to rename an existing folder.  Needless to say, to rely on this is very confusing,
        /// and needs to be changed.        
        ///
        /// <param name="folderID"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="parentID"></param>
        public bool UpdateFolder(InventoryFolderBase baseFolder)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            inventorySelect.GetProvider(m_userProfile.ID).SaveFolder(baseFolder);

            return true;
        }

        /// <summary>
        /// Handle an inventory folder move request from the client.
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        /// </summary>
        ///
        /// <param name="folderID"></param>
        /// <param name="parentID"></param>
        /// <returns>
        /// true if the delete was successful, or if it was queued pending folder receipt
        /// false if the folder to be deleted did not exist.
        /// </returns>
        public bool MoveFolder(UUID folderID, UUID parentID)
        {
            try
            {
                IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
                inventorySelect.GetCheckedProvider(m_userProfile.ID).MoveFolder(m_userProfile.ID, folderID, parentID);
                return true;
            }
            catch (InventoryStorageException)
            {
            }

            return false;
        }

        /// <summary>
        /// This method will delete all the items and folders in the given folder.
        /// </summary>
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        ///
        /// <param name="folderID"></param>
        public bool PurgeFolder(InventoryFolderBase folder)
        {
            try
            {
                IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
                inventorySelect.GetProvider(m_userProfile.ID).PurgeFolderContents(folder);

                return true;
            }
            catch (InventoryStorageException)
            {
            }

            return false;
        }

        /// <summary>
        /// Add an item to the user's inventory.
        /// </summary>
        /// If the item has no folder set (i.e. it is UUID.Zero), then it is placed in the most appropriate folder
        /// for that type.
        /// <param name="itemInfo"></param>
        public void AddItem(InventoryItemBase item)
        {
            if (item.Folder == UUID.Zero)
            {
                InventoryFolderBase f = FindFolderForType(item.AssetType);
                if (f != null)
                {
                    item.Folder = f.ID;
                }
                else
                {
                    InventoryFolderBase folder = FindFolderForType((int)AssetType.RootFolder);
                    item.Folder = folder.ID;
                }
            }

            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            selector.GetCheckedProvider(m_userProfile.ID).CreateItem(m_userProfile.ID, item);

            

            ItemReceive(item, null);
        }

        public InventoryItemBase ResolveLink(InventoryItemBase baseItem)
        {
            // Resolve Links if needed
            const int LINK_RECURSION_LIMIT = 32;
            int counter = 0;

            while ((baseItem != null) && (baseItem.AssetType == (int)AssetType.Link) && (++counter < LINK_RECURSION_LIMIT))
            {
                baseItem = this.FindItem(baseItem.AssetID);
            }

            if (baseItem.AssetType == (int)AssetType.Link)
            {
                //recursion limit was hit
                return null;
            }

            return baseItem;
        }

        /// <summary>
        /// Update an item in the user's inventory
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="itemInfo"></param>
        public void UpdateItem(InventoryItemBase item)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            selector.GetProvider(item.Owner).SaveItem(item);
        }

        /// <summary>
        /// Delete an item from the user's inventory
        ///
        /// If the inventory service has not yet delievered the inventory
        /// for this user then the request will be queued.
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns>
        /// true on a successful delete or a if the request is queued.
        /// Returns false on an immediate failure
        /// </returns>
        public bool DeleteItem(InventoryItemBase item)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            IInventoryStorage provider = selector.GetProvider(item.Owner);
            provider.PurgeItem(item);

            return true;
        }

        /// <summary>
        /// Send details of the inventory items and/or folders in a given folder to the client.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="folderID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <returns>true if the request was queued or successfully processed, false otherwise</returns>
        public bool SendInventoryDecendents(IClientAPI client, UUID folderID, bool fetchFolders, bool fetchItems)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            InventoryFolderBase folder = selector.GetCheckedProvider(client.AgentId).GetFolder(client.AgentId, folderID);

            List<InventoryFolderBase> subFolders = new List<InventoryFolderBase>();
            List<InventoryItemBase> items = new List<InventoryItemBase>();

            //sort into items and folders
            foreach (InventorySubFolderBase subFolder in folder.SubFolders)
            {
                subFolders.Add(new InventoryFolderBase { 
                    ID = subFolder.ID, ParentID = folderID, Name = subFolder.Name, Owner = subFolder.Owner, Type = subFolder.Type });
            }

            items.AddRange(folder.Items);

            client.SendInventoryFolderDetails(client.AgentId, folder, items, subFolders, fetchFolders, fetchItems);

            return true;
        }

        /// <summary>
        /// Find an appropriate folder for the given asset type
        /// </summary>
        /// <param name="type"></param>
        /// <returns>null if no appropriate folder exists</returns>
        public InventoryFolderBase FindFolderForType(int type)
        {
            InventoryFolderBase bestFolderForType;

            lock (_folderTypeCache)
            {
                _folderTypeCache.TryGetValue(type, out bestFolderForType);

                if (bestFolderForType != null) return bestFolderForType;
            }

            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            IInventoryStorage provider = inventorySelect.GetProvider(m_userProfile.ID);

            try
            {
                bestFolderForType = provider.FindFolderForType(m_userProfile.ID, (AssetType)type);
            }
            catch
            { }

            if (bestFolderForType == null)
            {
                //next best folder will be the user root folder, it has to exist
                try
                {
                    bestFolderForType = provider.FindFolderForType(m_userProfile.ID, AssetType.RootFolder);
                }
                catch
                { }

                if (bestFolderForType == null)
                {
                    throw new InventoryStorageException(
                        String.Format("Can not retrieve a destination folder for types, user {0} has no root folder", m_userProfile.ID));
                }
            }

            lock (_folderTypeCache)
            {
                if (_folderTypeCache.ContainsKey(type))
                {
                    _folderTypeCache[type] = bestFolderForType;
                }
                else
                {
                    _folderTypeCache.Add(type, bestFolderForType);
                }
            }

            return bestFolderForType;
        }

        // Searches the parentage tree for an ancestor folder with a matching type (e.g. Trash)
        public InventoryFolderBase FindTopLevelFolderFor(UUID folderID)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            IInventoryStorage provider = inventorySelect.GetProvider(m_userProfile.ID);
            InventoryFolderBase folder;
            try
            {
                folder = provider.FindTopLevelFolderFor(m_userProfile.ID, folderID);
            }
            catch
            {
                folder = null;
            }

            return folder;

        }

        // Load additional items that other regions have put into the database
        // The item will be added tot he local cache. Returns true if the item
        // was found and can be sent to the client
        //
        public bool QueryItem(UUID itemId)
        {
            try
            {
                IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
                InventoryItemBase itemInfo = inventorySelect.GetCheckedProvider(m_userProfile.ID).GetItem(m_userProfile.ID, itemId, UUID.Zero);
                if (itemInfo != null)
                {
                    ItemReceive(itemInfo, null);
                    return true;
                }
            }
            catch (InventoryStorageException)
            {
            }

            return false;
        }

        public bool QueryFolder(InventoryFolderBase folder)
        {
            return true; 
        }

        public void MoveItemToTrash(InventoryItemBase item, InventoryFolderBase trashFolder)
        {
            IInventoryProviderSelector inventorySelect = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            inventorySelect.GetProvider(item.Owner).SendItemToTrash(item, trashFolder.ID);
        }

        public void CheckedDeleteItem(UUID userId, UUID inventoryID)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            ICheckedInventoryStorage provider = selector.GetCheckedProvider(userId);
            provider.PurgeItem(userId, inventoryID);
        }

        public InventoryFolderBase GetFolder(UUID folderId)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            ICheckedInventoryStorage provider = selector.GetCheckedProvider(m_userProfile.ID);

            return provider.GetFolder(m_userProfile.ID, folderId);
        }

        public void MoveItem(UUID itemId, UUID folderID)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            ICheckedInventoryStorage provider = selector.GetCheckedProvider(m_userProfile.ID);
            provider.MoveItem(m_userProfile.ID, itemId, folderID);
        }

        public void ModifyAndMoveItem(InventoryItemBase item, UUID folderID)
        {
            IInventoryProviderSelector selector = ProviderRegistry.Instance.Get<IInventoryProviderSelector>();
            ICheckedInventoryStorage provider = selector.GetCheckedProvider(m_userProfile.ID);
            provider.ModifyAndMoveItem(m_userProfile.ID, item, folderID);
        }
    }

    /// <summary>
    /// Should be implemented by callers which require a callback when the user's inventory is received
    /// </summary>
    public interface IInventoryRequest
    {
        /// <summary>
        /// This is the method executed once we have received the user's inventory by which the request can be fulfilled.
        /// </summary>
        void Execute();
    }

    /// <summary>
    /// Generic inventory request
    /// </summary>
    class InventoryRequest : IInventoryRequest
    {
        private Delegate m_delegate;
        private Object[] m_args;

        internal InventoryRequest(Delegate delegat, Object[] args)
        {
            m_delegate = delegat;
            m_args = args;
        }

        public void Execute()
        {
            if (m_delegate != null)
                m_delegate.DynamicInvoke(m_args);
        }
    }
}
