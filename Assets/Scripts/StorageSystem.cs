using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Utils;
using static StorageSystem;
using static StorageItemsData;
using System;

public class StorageSystem : MonoBehaviour
{
    [Serializable]
    public class Storage
    {
        public int width;
        public int height;
        public Slot[,] slots;
        public List<Action> history;

        private int currLocalId = -1;
        private int NewLocalId
        {
            get
            {
                currLocalId++;
                return currLocalId;
            }
        }

        /// <summary>
        /// Storage class used to implement item storage (players' inventories, players' stashes, item containers)
        /// </summary>
        /// <param name="width">number of slots columns in the storage</param>
        /// <param name="height">number of slots rows in the storage</param>
        /// <param name="lockedSlots">boolean matrix containing the value true at the coordinates of each locked slots</param>
        public Storage(int width, int height, bool[,] lockedSlots = null)
        {
            this.width = width;
            this.height = height;
            slots = new Slot[width, height];
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    slots[i, j] = new Slot(i, j, this, lockedSlots != null ? lockedSlots[i, j] : false);
                }
            }
            history = new List<Action>();
        }

        /// <summary>
        /// Check if the slot position is within storage bounds and isn't locked
        /// </summary>
        public bool SlotInBounds(int x, int y)
        {
            return IsWithin(0, x, this.width - 1) && IsWithin(0, y, this.height - 1) && !slots[x, y].locked;
        }

        /// <summary>
        /// Get the slots in the defined area
        /// </summary>
        /// <param name="x">x position of the area top left slot</param>
        /// <param name="y">y position of the area top left slot</param>
        /// <param name="areaWidth">number of columns of the area</param>
        /// <param name="areaHeight">number of rows of the area</param>
        /// <returns>An matrix containing the area slots</returns>
        public Slot[,] AreaSlots(int x, int y, int areaWidth, int areaHeight)
        {
            Slot[,] area = new Slot[areaWidth, areaHeight];
            for (int i = 0; i < areaWidth; i++)
            {
                for (int j = 0; j < areaHeight; j++)
                {
                    area[i, j] = SlotInBounds(x + i, y + j) ? slots[x + i, y + j] : null;
                }
            }
            return area;
        }

        /// <summary>
        /// Check if all slots in the area are free (not locked and not full)
        /// </summary>
        /// <param name="area">The slots area matrix</param>
        public bool AreaFree(Slot[,] area)
        {
            if(area.GetLength(0) < 1 || area.GetLength(1) < 1) return false;

            for (int i = 0; i < area.GetLength(0); i++)
            {
                for (int j = 0; j < area.GetLength(1); j++)
                {
                    if (area[i,j] == null || !area[i, j].IsFree) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Place an item to the given position
        /// </summary>
        /// <param name="x">The x position where the top left slot of the item should be placed</param>
        /// <param name="y">The y position where the top left slot of the item should be placed</param>
        /// <param name="item">The item to place</param>
        /// <returns>True if the item was successfully placed</returns>
        public bool PlaceItem(int x, int y, Item item, bool saveAction = true)
        {
            int itemWidth = item.Width;
            int itemHeight = item.Height;
            Slot[,] area = AreaSlots(x,y,itemWidth,itemHeight);

            if (!AreaFree(area)) return false;

            for (int i = 0; i < itemWidth; i++)
            {
                for (int j = 0; j < itemHeight; j++)
                {
                    area[i, j].PlaceItem(item);
                }
            }
            item.Place(area);

            if(saveAction)
            {
                //TODO: make sure that reverting actions doesn't cause incoherent states with regards to this localId of an item
                item.localId = NewLocalId; //If this is a direct action, then the item is placed for the first time in the inventory, so we assign it a localId
                history.Add(new Action(Action.ActionType.Add, item, x, y));
            }

            return true;
        }

        /// <summary>
        /// Remove and item from the storage
        /// </summary>
        /// <param name="x">x coordinate of a slot containing the item to remove</param>
        /// <param name="y">y coordinate of a slot containing the item to remove</param>
        /// <returns>True if the item was successfully removed from the storage</returns>
        public bool RemoveItem(int x, int y, bool saveAction = true)
        {
            Item item = slots[x, y].item;

            if(item == null) return false;

            int itemWidth = item.Width;
            int itemHeight = item.Height;
            Slot[,] area = item.parents;

            if (AreaFree(area)) return false; //TODO: does this check make any sense ??

            Vector2Int topLeft = item.topLeft; //store the topLeft slot position before removing the item

            for (int i = 0; i < itemWidth; i++)
            {
                for (int j = 0; j < itemHeight; j++)
                {
                    area[i, j].RemoveItem();
                }
            }
            item.Remove();

            if (saveAction) history.Add(new Action(Action.ActionType.Remove, item, topLeft.x, topLeft.y));

            return true;
        }

        /// <summary>
        /// Move item to the given position in storage, if no storage is given the item is placed in the same storage it is moved from
        /// </summary>
        /// <param name="x">The x slot position of a slot containing the item to move</param>
        /// <param name="y">The y slot position of a slot containing the item to move</param>
        /// <param name="newX">The desired x top left slot position for the item in the destination storage</param>
        /// <param name="newY">The desired y top left slot position for the item in the destination storage</param>
        /// <param name="storage">The destination storage for the item, default is the item's original storage</param>
        /// <returns>True if the item was moved successfully</returns>
        public bool MoveItem(int x, int y, int newX, int newY, bool saveAction = true, Storage destStorage = null)
        {
            Debug.Log($"Moving item from Storage {this.GetHashCode()} to Storage {(destStorage != null ? destStorage : this).GetHashCode()}, from position ({x},{y}) to ({newX},{newY}) while {(saveAction ? "" : "not")} saving action");
            Item item = slots[x, y].item;

            if (item.topLeft.x == newX && item.topLeft.y == newY) return false; //We're not performing identity actions
            
            Vector2Int topLeft = item.topLeft; //store the topLeft slot position before removing the item

            if (!RemoveItem(x, y, false)) return false;

            destStorage ??= this;
            if (!destStorage.PlaceItem(newX, newY, item, false))
            {
                PlaceItem(x, y, item, false);
                return false;
            }

            if (saveAction) history.Add(new Action(Action.ActionType.Move, item, topLeft.x, topLeft.y, newX, newY, destStorage));

            return true;
        }

        //TODO: get rid of this method, it will cause trouble when setting up networking of actions (or rewrite it accordingly)
        /// <summary>
        /// Switch items position in the current storage
        /// </summary>
        /// <param name="item1">First item</param>
        /// <param name="item2">Second item</param>
        /// <returns>True if the item was moved successfully</returns>
        public bool SwitchItems(Item item1, Item item2)
        {
            if(item1.parents == null || item2.parents == null || item1.parents.GetLength(0)*item1.parents.GetLength(1)<1 || item2.parents.GetLength(0) * item2.parents.GetLength(1) < 1)
            {
                return false;
            }

            int x1 = item1.parents[0,0].x;
            int y1 = item1.parents[0,0].y;
            int x2 = item2.parents[0,0].x;
            int y2 = item2.parents[0,0].y;

            if (!RemoveItem(x1, y1, false)) //we try to remove item1 from its slots
            {
                return false;
            }
            if (!RemoveItem(x2, y2, false)) //we try to remove item2 from its slots knowing that item1 was successfully removed from its slots..
            {
                PlaceItem(x1, y1, item1, false); //..if it fails, we put item1 back to its place and return
                return false;
            }

            if(!PlaceItem(x2, y2, item1, false)) //we try to place item1 into item2 slots knowing that both items were successfully removed from their slots..
            {
                PlaceItem(x1, y1, item1, false); //..if it fails we put both items back to their respectives places
                PlaceItem(x2, y2, item2, false);
                return false;
            }

            if (!PlaceItem(x1, y1, item2, false)) //we try to place item2 into item1 slots knowing that item1 was successfully put into item2 slots
            {
                RemoveItem(x2, y2, false); //if it fails we remove item1 from item2 slots and we put both items back to their respectives places..
                PlaceItem(x1, y1, item1, false);
                PlaceItem(x2, y2, item2, false);
                return false;
            }

            return true; //..otherwise we successfully switched item1 and item2
        }

        /// <summary>
        /// Attempts to revert the Storage back to its state before the Action passed as parameter was performed
        /// </summary>
        /// <param name="target">The latest action to revert, default value set to last action</param>
        /// <returns>True if Actions were successfully undone, False if things went very wrong and we are screwed</returns>
        public bool Undo(Action target = null)
        {
            if (history.Count == 0) return false;

            int targetIndex = target != null ? history.IndexOf(target) : history.Count - 1;
            if (targetIndex == -1) return false;

            for (int i = history.Count - 1; i >= targetIndex; i--)
            {
                Action action = history[i];
                history.RemoveAt(i);

                switch (action.type)
                {
                    case Action.ActionType.Move:
                        if (!action.destStorage.MoveItem(action.newX, action.newY, action.x, action.y, false, this))
                        {
                            return false;
                        }
                        break;
                    case Action.ActionType.Remove:
                        if (!PlaceItem(action.x, action.y, action.item, false))
                        {
                            return false;
                        }
                        break;
                    case Action.ActionType.Add:
                        if (!RemoveItem(action.x, action.y, false))
                        {
                            return false;
                        }
                        break;
                    default:
                        return false;
                }
            }

            return true;
        }

    }

    [Serializable]
    public class Slot
    {
        public int x;
        public int y;
        public bool locked;
        public bool full;
        public Item item;
        public Storage parent;

        public Slot(int x, int y, Storage parent, bool locked = false)
        {
            this.x = x;
            this.y = y;
            this.parent = parent;
            this.locked = locked;
        }

        /// <summary>
        /// Returns true when the slot is free (not locked and not full)
        /// </summary>
        public bool IsFree => !(locked || full);

        public void PlaceItem(Item item)
        {
            full = true;
            this.item = item;
        }

        public void RemoveItem()
        {
            this.item = null;
            full = false;
        }
    }

    [Serializable]
    public class Item
    {
        public int id;
        public int localId;
        public int amount;
        public bool rotated;
        public Slot[,] parents;
        public Vector2Int topLeft; //the x and y slot position of the top left slot containing the item if there is one, otherwise (-1, -1)

        public Item(int id, int amount, bool rotated = false, Slot[,] parents = null)
        {
            this.id = id;
            this.amount = amount;
            this.rotated = rotated;
            this.parents = parents;
            topLeft = new Vector2Int(-1, -1);
        }

        /// <summary>
        /// returns the relative width of the item depending on its rotation
        /// </summary>
        public int Width => rotated ? itemsData[id].width : itemsData[id].height;
        /// <summary>
        /// returns the relative height of the item depending on its rotation
        /// </summary>
        public int Height => rotated ? itemsData[id].height : itemsData[id].width;

        public void Place(Slot[,] area)
        {
            parents = area;
            topLeft = new Vector2Int(area[0, 0].x, area[0, 0].y);
        }

        public void Remove()
        {
            parents = null;
            topLeft = new Vector2Int(-1, -1);
        }
    }

    [Serializable]
    public class Action
    {
        public enum ActionType { Move, Remove, Add }
        public ActionType type;
        public Item item;
        public int x, y, newX, newY;
        public Storage destStorage;

        public Action(ActionType type, Item item, int x, int y, int newX = -1, int newY = -1, Storage destStorage = null)
        {
            this.type = type;
            this.item = item;
            this.x = x;
            this.y = y;
            this.newX = newX;
            this.newY = newY;
            this.destStorage = destStorage;
        }
    }

}
