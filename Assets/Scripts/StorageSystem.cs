using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Utils;
using static StorageSystem;
using static StorageItemsData;

public class StorageSystem : MonoBehaviour
{
    public class Storage
    {
        public int width;
        public int height;
        public Slot[,] slots;

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
            if(area.GetLength(0) < 1 || area.GetLength(1) < 1)
            {
                return false;
            }

            for (int i = 0; i < area.GetLength(0); i++)
            {
                for (int j = 0; j < area.GetLength(1); j++)
                {
                    if (area[i,j] == null || !area[i, j].IsFree)
                    {
                        return false;
                    }
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
        public bool PlaceItem(int x, int y, Item item)
        {
            int itemWidth = item.Width;
            int itemHeight = item.Height;
            Slot[,] area = AreaSlots(x,y,itemWidth,itemHeight);

            if (!AreaFree(area))
            {
                return false;
            }

            for (int i = 0; i < itemWidth; i++)
            {
                for (int j = 0; j < itemHeight; j++)
                {
                    area[i, j].PlaceItem(item);
                }
            }
            item.Place(area);
            return true;
        }

        /// <summary>
        /// Remove and item from the storage
        /// </summary>
        /// <param name="x">x coordinate of a slot containing the item to remove</param>
        /// <param name="y">y coordinate of a slot containing the item to remove</param>
        /// <returns>True if the item was successfully removed from the storage</returns>
        public bool RemoveItem(int x, int y)
        {
            Item item = slots[x, y].item;

            if(item == null)
            {
                return false;
            }

            int itemWidth = item.Width;
            int itemHeight = item.Height;
            Slot[,] area = item.parents;

            if (AreaFree(area))
            {
                return false;
            }

            for (int i = 0; i < itemWidth; i++)
            {
                for (int j = 0; j < itemHeight; j++)
                {
                    area[i, j].RemoveItem();
                }
            }
            item.Remove();
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
        public bool MoveItem(int x, int y, int newX, int newY, Storage storage=this)
        {
            Item item = slots[x, y].item;
            
            if(!RemoveItem(x, y))
            {
                return false;
            }

            if (!storage.PlaceItem(newX, newY, item))
            {
                PlaceItem(x, y, item);
                return false;
            }
            return true;
        }

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

            if (!RemoveItem(x1, y1)) //we try to remove item1 from its slots
            {
                return false;
            }
            if (!RemoveItem(x2, y2)) //we try to remove item2 from its slots knowing that item1 was successfully removed from its slots..
            {
                PlaceItem(x1, y1, item1); //..if it fails, we put item1 back to its place and return
                return false;
            }

            if(!PlaceItem(x2, y2, item1)) //we try to place item1 into item2 slots knowing that both items were successfully removed from their slots..
            {
                PlaceItem(x1, y1, item1); //..if it fails we put both items back to their respectives places
                PlaceItem(x2, y2, item2);
                return false;
            }

            if (!PlaceItem(x1, y1, item2)) //we try to place item2 into item1 slots knowing that item1 was successfully put into item2 slots
            {
                RemoveItem(x2, y2); //if it fails we remove item1 from item2 slots and we put both items back to their respectives places..
                PlaceItem(x1, y1, item1);
                PlaceItem(x2, y2, item2);
                return false;
            }

            return true; //..otherwise we successfully switched item1 and item2
        }
    }

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

    public class Item
    {
        public int id;
        public int amount;
        public bool rotated;
        public Slot[,] parents;

        public Item(int id, int amount, bool rotated = false, Slot[,] parents = null)
        {
            this.id = id;
            this.amount = amount;
            this.rotated = rotated;
            this.parents = parents;
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
        }

        public void Remove()
        {
            parents = null;
        }
    }

    public class Action
    {
        public int id;
        public Storage oldStorage;
        public Storage destStorage;
        public Item item;
        private int code = -1;

        public Action(int id, Storage oldStorage, Storage destStorage, Item item)
        {
            this.id = id;
            this.oldStorage = oldStorage;
            this.destStorage = destStorage;
        }


        public int ActionCode
        {
            get
            {
                if (code == -1)
                {
                    //compute code

                }
                return code;
            }
        }
    }
}
