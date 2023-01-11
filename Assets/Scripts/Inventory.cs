using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static StorageSystem;
using static StorageItemsData;
using UnityEngine.Rendering;

public class Inventory : MonoBehaviour
{
    private Storage inventory;

    public int cellSize = 40;
    public int cellSpacing = 5;

    public Vector2 inventoryTopLeftOffset = new Vector2(10, 10);

    private int currId = 0;
    private bool currRot = false;
    private bool currMode = false;

    private void Start()
    {
        inventory = new Storage(10, 8);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            currId = currId == 0 ? 1 : 0;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            currRot = !currRot;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            currMode = !currMode;
        }

        Vector2Int pos = MousePosInInventory;
        int i = pos.x;
        int j = pos.y;
        if ((Input.GetButtonDown("Fire1") || Input.GetButtonDown("Fire2")) && !inventory.slots[i, j].locked)
        {
            if (Input.GetButtonDown("Fire1"))
            {
                print(inventory.PlaceItem(i, j, new Item(currId, 1, currRot)));
            }

            if (Input.GetButtonDown("Fire2"))
            {
                print(inventory.RemoveItem(i, j));
            }
        }
    }

    private void OnGUI()
    {
        for (int i = 0; i < inventory.width; i++)
        {
            for (int j = 0; j < inventory.height; j++)
            {
                GUI.Box(new Rect(inventoryTopLeftOffset.x + i * (cellSize + cellSpacing), inventoryTopLeftOffset.y + j * (cellSize + cellSpacing), cellSize, cellSize), inventory.slots[i, j].full ? $"{inventory.slots[i, j].item.id}" : "");
            }
        }

        GUILayout.Box($"curr Item: {currId}");
        GUILayout.Box($"curr Rot: {currRot}");
        GUILayout.Box($"curr Mode: {currMode}");
        GUILayout.Box($"fire1: {Input.GetButton("Fire1")}");
        GUILayout.Box($"fire2: {Input.GetButton("Fire2")}");
        GUILayout.Box($"MousePosInInventory: {MousePosInInventory}");
    }

    private Vector2 MousePos
    {
        get
        {
            Vector2 input = Input.mousePosition;
            input.y = Screen.height - input.y;
            return input;
        }
    }

    public Vector2Int MousePosInInventory
    {
        get
        {
            Vector2 input = MousePos - inventoryTopLeftOffset - Vector2.one * ((cellSize + cellSpacing) / 2f);
            input /= (cellSize + cellSpacing);
            return new Vector2Int(Mathf.RoundToInt(input.x), Mathf.RoundToInt(input.y));
        }
    }
}
