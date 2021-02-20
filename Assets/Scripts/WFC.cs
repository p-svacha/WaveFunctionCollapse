using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;
using UnityEngine.UI;

public class WFC : MonoBehaviour
{
    public Texture2D InputTexture;
    public Image OriginalImage;
    public Image GeneratedImage;
    public Text SizeText;
    public Text RuleVectorSize;

    public List<Rule> Rules = new List<Rule>();
    public List<Color> Colors = new List<Color>();

    public Color Outside = new Color(0, 0, 0, 0.01f);
    public Color Unset = new Color(0.5f, 0.5f, 0.5f, 0.9f);

    // Output
    private bool Generating;
    private int OutputWidth;
    private int OutputHeight;
    public Color[,] OutputPixels;

    // Candidates
    public Dictionary<Color, int>[,] PixelCandidates; // x/y are the coordinates of the pixel. The dictionary describes which color could appear how likely
    public Dictionary<int, List<Vector2>> PixelsByNumCandidates; // Key is amount of candidates, Value is pixel coordinate

    private int LastRuleVectorSize = 0;
    private List<Vector2> RuleVectors = new List<Vector2>();

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if(Generating)
        {
            if (!SetNextPixel()) Generating = false;
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            int rvSize = int.Parse(RuleVectorSize.text);
            if (rvSize != LastRuleVectorSize)
            {
                CreateRuleVectors(rvSize);
                ReadInputImage();
                SetOriginalTexture();
                LastRuleVectorSize = rvSize;
            }
            int size = int.Parse(SizeText.text);
            InitImageGeneration(size, size);
            Generating = true;
        }
        /*
        else if(Input.GetKeyDown(KeyCode.P))
        {
            SetNextPixel();
        }
        */
    }

    private void CreateRuleVectors(int rvSize)
    {
        for(int x = -rvSize; x <= rvSize; x++)
        {
            for(int y = -rvSize; y <= rvSize; y++)
            {
                if(Math.Abs(x) + Math.Abs(y) <= rvSize)
                {
                    RuleVectors.Add(new Vector2(x, y));
                }
            }
        }
    }

    private void ReadInputImage()
    {
        Colors.Clear();
        Rules.Clear();
        for(int y = 0; y < InputTexture.height; y++)
        {
            for(int x = 0; x < InputTexture.width; x++)
            {
                CreateRulesForPixel(x, y);
            }
        }
        Debug.Log("Source Image succesfully loaded, containing " + Colors.Count + " colors and " + Rules.Count + " rules.");
    }


    private void CreateRulesForPixel(int x, int y)
    {
        Color pixel = InputTexture.GetPixel(x, y);
        if (!Colors.Contains(pixel)) Colors.Add(pixel);

        foreach(Vector2 rv in RuleVectors)
        {
            Color targetPixel;
            if (x + rv.x < 0 || x + rv.x >= InputTexture.width || y + rv.y < 0 || y + rv.y >= InputTexture.height) targetPixel = Outside;
            else targetPixel = InputTexture.GetPixel(x + (int)rv.x, y + (int)rv.y);

            CreateRule(pixel, targetPixel, (int)rv.x, (int)rv.y);
        }
    }

    private void CreateRule(Color c1, Color c2, int dx, int dy)
    {
        Rule NewRule = new Rule(c1, c2, dx, dy);
        Rule existingRule = null;
        foreach(Rule r in Rules)
        {
            
            if (r.Color1.r == c1.r && r.Color1.g == c1.g && r.Color1.b == c1.b && r.Color1.a == c1.a &&
                r.Color2.r == c2.r && r.Color2.g == c2.g && r.Color2.b == c2.b && r.Color2.a == c2.a &&
                r.dx == dx && r.dy == dy) existingRule = r;
            
        }
        if (existingRule == null) Rules.Add(NewRule);
        else existingRule.Occurences++;
    }

    private void SetOriginalTexture()
    {
        OriginalImage.sprite = Sprite.Create(InputTexture, new Rect(0, 0, InputTexture.width, InputTexture.height), new Vector2(0.5f, 0.5f));
    }

    private void DebugRules()
    {
        foreach(Rule rule in Rules)
        {
            Debug.Log(rule.Color1 + " can be " + rule.dx + "/" + rule.dy + " from " + rule.Color2);
        }
    }


    private void InitImageGeneration(int width, int height)
    {
        OutputWidth = width;
        OutputHeight = height;

        OutputPixels = new Color[width, height];
        PixelCandidates = new Dictionary<Color, int>[width, height];

        PixelsByNumCandidates = new Dictionary<int, List<Vector2>>();
        for (int i = 0; i <= Colors.Count; i++)
        {
            PixelsByNumCandidates.Add(i, new List<Vector2>());
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                OutputPixels[x, y] = Unset;
                PixelCandidates[x, y] = new Dictionary<Color, int>();
                foreach(Color c in Colors)
                {
                    PixelCandidates[x, y].Add(c, 1);
                }
                
            }
        }

        for (int y = - 1; y < height + 1; y++)
        {
            for (int x = -1; x < width + 1; x++)
            {
                if (x == -1 || y == -1 || y == height || x == width) SetPixel(x, y, Outside);
            }
        }

        UpdateGeneratedImage();
    }

    private void SetPixel(int x, int y, Color color)
    {
        if(x >= 0 && x < OutputWidth && y >= 0 && y < OutputHeight) OutputPixels[x, y] = color;
        foreach (Vector2 rv in RuleVectors)
        {
            int targetX = x + (int)rv.x;
            int targetY = y + (int)rv.y;
            if (targetX >= 0 && targetX < OutputWidth && targetY >= 0 && targetY < OutputHeight) // Pixel is inside picture -> Update candidates
            {
                Color pixelColor = OutputPixels[targetX, targetY];

                if (pixelColor == Unset)
                {
                    // Remove from candidate dictionary
                    if (PixelsByNumCandidates[PixelCandidates[targetX, targetY].Where(d => d.Value > 0).Count()].Contains(new Vector2(targetX, targetY)))
                    {
                        PixelsByNumCandidates[PixelCandidates[targetX, targetY].Where(d => d.Value > 0).Count()].Remove(new Vector2(targetX, targetY));
                    }

                    // Find Rules
                    List<Rule> affectingRules = Rules.Where(r => r.Color2 == color && r.dx == -rv.x && r.dy == -rv.y).ToList(); //TODO: - / +
                    if (affectingRules.Count > 0)
                    {
                        List<Color> possibleColors = affectingRules.Select(r => r.Color1).ToList();

                        foreach(Color c in Colors)
                        {
                            List<Rule> colorAffectingRules = affectingRules.Where(r => r.Color1 == c).ToList();
                            if (colorAffectingRules.Count > 0 && PixelCandidates[targetX, targetY][c] > 0)
                            {
                                PixelCandidates[targetX, targetY][c] += colorAffectingRules.Sum(r => r.Occurences);
                            }
                            else
                            {
                                PixelCandidates[targetX, targetY][c] = 0;
                            }
                        }
                    }

                    // Add to candidate dictionary
                    PixelsByNumCandidates[PixelCandidates[targetX, targetY].Where(d => d.Value > 0).Count()].Add(new Vector2(targetX, targetY));
                }
            }
        }
    }

    /// <summary>
    /// Sets one pixel of the image. Returns true if set is successfull, false if not (either image complete or out of options)
    /// </summary>
    private bool SetNextPixel()
    {
        // Find next pixel
        int numCandidates = 1;
        while(PixelsByNumCandidates[numCandidates].Count == 0 && numCandidates < PixelsByNumCandidates.Count - 1)
        {
            numCandidates++;
        }
        if (numCandidates == PixelsByNumCandidates.Count - 1) return false;
        Vector2 nextPixel = PixelsByNumCandidates[numCandidates][UnityEngine.Random.Range(0, PixelsByNumCandidates[numCandidates].Count)];
        PixelsByNumCandidates[numCandidates].Remove(nextPixel);

        // Chose a color weighted random
        Dictionary<Color, int> colorCandidates = PixelCandidates[(int)nextPixel.x, (int)nextPixel.y];
        Color chosenColor = GetWeightedRandomColor(colorCandidates);

        /*
        // Debug
        Debug.Log("Generating Pixel at " + nextPixel.x + "/" + nextPixel.y + ": " + colorCandidates.Where(x => x.Value > 0).Count() + " Candidates:");
        foreach (KeyValuePair<Color, int> c in colorCandidates.Where(x => x.Value > 0))
        {
            Debug.Log(c.Key + ": " + c.Value * 100 / colorCandidates.Sum(x => x.Value) + "%");
        }
        */

        // Update Rules
        SetPixel((int)nextPixel.x, (int)nextPixel.y, chosenColor);

        // Draw Image
        UpdateGeneratedImage();

        return true;
    }

    private Color GetWeightedRandomColor(Dictionary<Color, int> colors)
    {
        int rng = UnityEngine.Random.Range(0, colors.Sum(x => x.Value));
        int sum = 0;
        foreach(KeyValuePair<Color, int> kvp in colors)
        {
            sum += kvp.Value;
            if (rng < sum) return kvp.Key;
        }
        return Color.red;
    }

    private void UpdateGeneratedImage()
    {
        Texture2D texture = new Texture2D(OutputWidth, OutputHeight);
        texture.filterMode = FilterMode.Point;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, OutputPixels[x, y]);
            }
        }
        texture.Apply();
        GeneratedImage.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
