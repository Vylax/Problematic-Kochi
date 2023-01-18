using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Utils : MonoBehaviour
{
    public enum SceneId
    {
        MainMenu = 0
    }

    public static bool IsWithin(float a, float x, float b, bool inclusive = true)
    {
        return (a < x && x < b) || inclusive && (a == x || x == b); 
    }
}
