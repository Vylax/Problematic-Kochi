using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageItemsData : MonoBehaviour
{
    public static Dictionary<int,ItemsData> itemsData = new Dictionary<int, ItemsData>
    {
        { 0, new ItemsData(0, "test", "", -1, 2, 1) },
        { 1, new ItemsData(1, "test2", "", -1, 1, 1) }
    };

    public class ItemsData
    {
        public int id;
        public string name;
        public string description;
        public int type;
        public int width;
        public int height;
        public Texture2D icon;

        public ItemsData(int id, string name, string description, int type, int width, int height, Texture2D icon = null)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.type = type;
            this.width = width;
            this.height = height;
            this.icon = icon;
        }
    }
}
