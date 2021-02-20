using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rule
{
    public Color Color1;
    public Color Color2;
    public int dx;
    public int dy;
    public int Occurences;


    // C1 can be at dx/dy from c2
    public Rule(Color c1, Color c2, int dx, int dy)
    {
        Color1 = c1;
        Color2 = c2;
        this.dx = dx;
        this.dy = dy;
        Occurences = 1;
    }
}
