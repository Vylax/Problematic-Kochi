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

        public bool SlotInBounds(int x, int y)
        {
            return IsWithin(0, x, this.width - 1) && IsWithin(0, y, this.height - 1) && !slots[x, y].locked;
        }

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

        public bool MoveItem(int x, int y, int newX, int newY)
        {
            Item item = slots[x, y].item;
            
            if(!RemoveItem(x, y))
            {
                return false;
            }

            if (!PlaceItem(newX, newY, item))
            {
                PlaceItem(x, y, item);
                return false;
            }
            return true;
        }

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

        public int Width => rotated ? itemsData[id].width : itemsData[id].height;
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
}
