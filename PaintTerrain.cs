using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEngine.Networking;
using DG.Tweening;
using JimmysUnityUtilities;

public class PaintTerrain
{
    private GameObject spider;
    public LayerMask terrainLayerMask;
    private Terrain targetTerrain; //The terrain obj you want to edit

    float[,] terrainHeightMap;  //a 2d array of floats to store 
    int terrainHeightMapWidth; //Used to calculate click position
    int terrainHeightMapHeight;
    float[,] heights; //a variable to store the new heights

    TerrainData targetTerrainData; // stores the terrains terrain data
    TerrainCollider terrainCollider;



    public enum EffectType
    {
        raise,
        lower,
        flatten,
        smooth,
        paint,
    };
    public Texture2D[] brushIMG; // This will allow you to switch brushes

    float[,] brush; // this stores the brush.png pixel data
    float[,] brush1; // this stores the brush.png pixel data
    float[,] brush2; // this stores the brush.png pixel data

    private int currentBrush = 1;
    public int brushSelection=0; // current selected brush
    public int areaOfEffectSize = 100; // size of the brush
    [Range(0.01f, 1f)] // you can remove this if you want
    public float strength; // brush strength
    public float flattenHeight = 0; // the height to which the flatten mode will go
    public EffectType effectType;
    public TerrainLayer[] paints;// a list containing all of the paints
    public int paint=0; // variable to select paint

    float[,,] splat; // A splat map is what unity uses to overlay all of your paints on to the terrain
    float[,,] CachedTerrainAlphamapData;

    public bool canPaint = false;
    private bool isDrawing = false;


    public void init()
    {
        //GameController.t.updateEvent.AddListener(update);
        effectType = EffectType.paint;

        spider = GameObject.Find("SPIDER");
        initTerrain();

        DOVirtual.DelayedCall(0.1f, update).SetLoops(-1);

    }

    public void initTerrain()
    {
        int _startArea = areaOfEffectSize;
        targetTerrain =MonoBehaviour.FindObjectOfType<Terrain>();   
        terrainCollider = MonoBehaviour.FindObjectOfType<TerrainCollider>();

        resetMap();

        brush = brush1;

        SetLayers(targetTerrain.terrainData);
        SetBrushSize(areaOfEffectSize*4);
        SetBrushStrength(0.6f);
        SetEditValues(targetTerrain);
        

        draw();

        areaOfEffectSize = _startArea;
        SetBrushSize(areaOfEffectSize);
    }

    public void resetMap()
    {
        Debug.Log("reset");
        float[,,] map = new float[
            targetTerrain.terrainData.alphamapWidth,
            targetTerrain.terrainData.alphamapHeight,
            paints.Length];

        for (int y = 0; y < targetTerrain.terrainData.alphamapHeight; y++)
        { 
            for (int x = 0; x < targetTerrain.terrainData.alphamapWidth; x++)
            {
              
                map[x, y, 0] = 0;
                map[x, y, 1] = 1;
            }
        }
        targetTerrain.terrainData.SetAlphamaps(0, 0, map);
    }

    public Terrain GetTerrainAtObject(GameObject gameObject)
    {
        if (gameObject.GetComponent<Terrain>())
        {
            //This will return the Terrain component of an object (if present)
            return gameObject.GetComponent<Terrain>();
        }
        return default(Terrain);
    }

    public TerrainData GetCurrentTerrainData()
    {
        if (targetTerrain)
        {
            return targetTerrain.terrainData;
        }
        return default(TerrainData);
    }

    public Terrain GetCurrentTerrain()
    {
        if (targetTerrain)
        {
            return targetTerrain;
        }
        return default(Terrain);
    }

    public void SetEditValues(Terrain terrain)
    {
        targetTerrainData = GetCurrentTerrainData();
        terrainHeightMap = GetCurrentTerrainHeightMap();
        terrainHeightMapWidth = GetCurrentTerrainWidth();
        terrainHeightMapHeight = GetCurrentTerrainHeight();
    }


    private void GetTerrainCoordinates(Vector3 _point, out int x, out int z)
    {
        int offset = areaOfEffectSize / 2; //This offsets the hit position to account for the size of the brush which gets drawn from the corner out
                                           //World Position Offset Coords, these can differ from the terrain coords if the terrain object is not at (0,0,0)
        Vector3 tempTerrainCoodinates = _point - targetTerrain.transform.position;
        //This takes the world coords and makes them relative to the terrain
        Vector3 terrainCoordinates = new Vector3(
            tempTerrainCoodinates.x / GetTerrainSize().x,
            tempTerrainCoodinates.y / GetTerrainSize().y,
            tempTerrainCoodinates.z / GetTerrainSize().z);
        // This will take the coords relative to the terrain and make them relative to the height map(which often has different dimensions)
        Vector3 locationInTerrain = new Vector3
            (
            terrainCoordinates.x * terrainHeightMapWidth,
            0,
            terrainCoordinates.z * terrainHeightMapHeight
            );
        //Finally, this will spit out the X Y values for use in other parts of the code
        x = (int)locationInTerrain.x - offset-1;
        z = (int)locationInTerrain.z - offset-1;
    }

    private float GetSurroundingHeights(float[,] height, int x, int z)
    {
        float value; // this will temporarily hold the value at each point
        float avg = height[x, z]; // we will add all the heights to this and divide by int num bellow to get the average height
        int num = 1;
        for (int i = 0; i < 4; i++) //this will loop us through the possible surrounding spots
        {
            try // This will try to run the code bellow, and if one of the coords is not on the terrain(ie we are at an edge) it will pass the exception to the Catch{} below
            {
                // These give us the values surrounding the point
                if (i == 0)
                { value = height[x + 1, z]; }
                else if (i == 1)
                { value = height[x - 1, z]; }
                else if (i == 2)
                { value = height[x, z + 1]; }
                else
                { value = height[x, z - 1]; }
                num++; // keeps track of how many iterations were successful  
                avg += value;
            }
            catch (System.Exception)
            {
            }
        }
        avg = avg / num;
        return avg;
    }

    public Vector3 GetTerrainSize()
    {
        if (targetTerrain)
        {
            return targetTerrain.terrainData.size;
        }
        return Vector3.zero;
    }

    public float[,] GetCurrentTerrainHeightMap()
    {
        if (targetTerrain)
        {
            // the first 2 0's indicate the coords where we start, the next values indicate how far we extend the area, so what we are saying here is I want the heights starting at the Origin and extending the entire width and height of the terrain
            return targetTerrain.terrainData.GetHeights(0, 0,
            targetTerrain.terrainData.heightmapResolution,
            targetTerrain.terrainData.heightmapResolution);
        }
        return default(float[,]);
    }

    public int GetCurrentTerrainWidth()
    {
        if (targetTerrain)
        {
            return targetTerrain.terrainData.heightmapResolution;
        }
        return 0;
    }
    public int GetCurrentTerrainHeight()
    {
        if (targetTerrain)
        {
            return targetTerrain.terrainData.heightmapResolution;
        }
        return 0;
        //test2.GetComponent<MeshRenderer>().material.mainTexture = texture;
    }

    public float[,] GenerateBrush(Texture2D texture, int size)
    {
       
        float[,] heightMap = new float[size, size];//creates a 2d array which will store our brush

         //Texture2D scaledBrush = ResizeBrush(texture, size, size); // this calls a function which we will write next, and resizes the brush image
        //This will iterate over the entire re-scaled image and convert the pixel color into a value between 0 and 1

        Texture2D scaledBrush = ScaleTexture(texture, size, size);
      //  Texture2D scaledBrush = Resize(texture, size, size);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Color pixelValue = scaledBrush.GetPixel(x, y);
                heightMap[x, y] = pixelValue.grayscale / 255;
            }
        }

        return heightMap;
    }
private Texture2D ScaleTexture(Texture2D source,int targetWidth,int targetHeight) {
    Texture2D result=new Texture2D(targetWidth,targetHeight,source.format,true);
	Color[] rpixels=result.GetPixels(0);
	float incX=(1.0f / (float)targetWidth);
	float incY=(1.0f / (float)targetHeight); 
	for(int px=0; px<rpixels.Length; px++) { 
		rpixels[px] = source.GetPixelBilinear(
            incX*((float)px%targetWidth),
             incY*((float)Mathf.Floor(px/targetWidth))); 
	} 

	result.SetPixels(rpixels,0); 
	result.Apply(); 
	return result; 
}
   /* Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
    {
        RenderTexture rt = new RenderTexture(targetX, targetY, 24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D, rt);
        Texture2D result = new Texture2D(targetX, targetY);
        result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
        result.Apply();
        RenderTexture.active =null;
        return result;
    }*/

   /* public static Texture2D ResizeBrush(Texture2D src, int width, int height, FilterMode mode = FilterMode.Trilinear)
    {
        Rect texR = new Rect(0, 0, width, height);
        _gpu_scale(src, width, height, mode);
        //Get rendered data back to a new texture
        Texture2D result = new Texture2D(width, height, TextureFormat.ARGB32, true);
        result.Resize(width, height);
        result.ReadPixels(texR, 0, 0, true);
        return result;
    }*/
    static void _gpu_scale(Texture2D src, int width, int height, FilterMode fmode)
    {
        //We need the source texture in VRAM because we render with it
        src.filterMode = fmode;
        src.Apply(true);
        //Using RTT for best quality and performance. Thanks, Unity 5
        RenderTexture rtt = new RenderTexture(width, height, 32);
        //Set the RTT in order to render to it
        Graphics.SetRenderTarget(rtt);
        //Setup 2D matrix in range 0..1, so nobody needs to care about sized
        GL.LoadPixelMatrix(0, 1, 1, 0);
        //Then clear & draw the texture to fill the entire RTT.
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        Graphics.DrawTexture(new Rect(0, 0, 1, 1), src);
    }

    public void SetPaint(int num)
    {
        paint = num;
    }
    public void SetLayers(TerrainData t)
    {
        t.terrainLayers = paints;
    }
    public void SetBrushSize(int value)//adds int value to brush size(make negative to shrink)
    {
        areaOfEffectSize = value;
        if (areaOfEffectSize > 200)
        { areaOfEffectSize = 200; }
        else if (areaOfEffectSize < 1)
        { areaOfEffectSize = 1; }

        brush1 = GenerateBrush(brushIMG[0], areaOfEffectSize);
        brush2 = GenerateBrush(brushIMG[1], areaOfEffectSize);

        brush = brush1; // regenerates the brush with new size
    }
    public void SetBrushStrength(float value)//same idea as SetBrushSize()
    {
        strength = value;
        if (strength > 1)
        { strength = 1; }
        else if (strength < 0.01f)
        { strength = 0.01f; }
    }
    public void SetBrush(int num)
    {
        brushSelection = num;
        brush = GenerateBrush(brushIMG[brushSelection], areaOfEffectSize);
       // RMC.SetIndicators();
    }

    void ModifyTerrain(int x, int z)
    {
        //These AreaOfEffectModifier variables below will help us if we are modifying terrain that goes over the edge, you will see in a bit that I use Xmod for the the z(or Y) values, which was because I did not realize at first that the terrain X and world X is not the same so I had to flip them around and was too lazy to correct the names, so don't get thrown off by that.
        int AOExMod = 0;
        int AOEzMod = 0;
        int AOExMod1 = 0;
        int AOEzMod1 = 0;
        if (x < 0) // if the brush goes off the negative end of the x axis we set the mod == to it to offset the edited area
        {
            AOExMod = x;
        }
        else if (x + areaOfEffectSize > terrainHeightMapWidth)// if the brush goes off the posative end of the x axis we set the mod == to this
        {
            AOExMod1 = x + areaOfEffectSize - terrainHeightMapWidth;
        }

        if (z < 0)//same as with x
        {
            AOEzMod = z;
        }
        else if (z + areaOfEffectSize > terrainHeightMapHeight)
        {
            AOEzMod1 = z + areaOfEffectSize - terrainHeightMapHeight;
        }
        if (effectType != EffectType.paint) // the following code will apply the terrain height modifications
        {
            heights = targetTerrainData.GetHeights(x - AOExMod, z - AOEzMod, areaOfEffectSize + AOExMod - AOExMod1, areaOfEffectSize + AOEzMod - AOEzMod1); // this grabs the heightmap values within the brushes area of effect
        }
        ///Raise Terrain
        if (effectType == EffectType.raise)
        {
            for (int xx = 0; xx < areaOfEffectSize + AOEzMod - AOEzMod1; xx++)
            {
                for (int yy = 0; yy < areaOfEffectSize + AOExMod - AOExMod1; yy++)
                {
                    heights[xx, yy] += brush[xx - AOEzMod, yy - AOExMod] * strength; //for each point we raise the value  by the value of brush at the coords * the strength modifier
                }
            }
            targetTerrainData.SetHeights(x - AOExMod, z - AOEzMod, heights); // This bit of code will save the change to the Terrain data file, this means that the changes will persist out of play mode into the edit mode
        }
        ///Lower Terrain, just the reverse of raise terrain
        else if (effectType == EffectType.lower)
        {
            for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
            {
                for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
                {
                    heights[xx, yy] -= brush[xx - AOEzMod, yy - AOExMod] * strength;
                }
            }
            targetTerrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
        }
        //this moves the current value towards our target value to flatten terrain
        else if (effectType == EffectType.flatten)
        {
            for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
            {
                for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
                {
                    heights[xx, yy] = Mathf.MoveTowards(heights[xx, yy], flattenHeight / 600, brush[xx - AOEzMod, yy - AOExMod] * strength);
                }
            }
            targetTerrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
        }
        //Takes the average of surrounding points and moves the point towards that height
        else if (effectType == EffectType.smooth)
        {
            float[,] heightAvg = new float[heights.GetLength(0), heights.GetLength(1)];
            for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
            {
                for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
                {
                    heightAvg[xx, yy] = GetSurroundingHeights(heights, xx, yy); // calculates the value we want each point to move towards
                }
            }
            for (int xx1 = 0; xx1 < areaOfEffectSize + AOEzMod; xx1++)
            {
                for (int yy1 = 0; yy1 < areaOfEffectSize + AOExMod; yy1++)
                {
                    heights[xx1, yy1] = Mathf.MoveTowards(heights[xx1, yy1], heightAvg[xx1, yy1], brush[xx1 - AOEzMod, yy1 - AOExMod] * strength); // moves the points towards their targets
                }
            }
            targetTerrainData.SetHeights(x - AOExMod, z - AOEzMod, heights);
        }
        //This is where we do the painting, sorry its buried so far in here
        else if (effectType == EffectType.paint)
        {
            splat = targetTerrain.terrainData.GetAlphamaps(x - AOExMod, z - AOEzMod, areaOfEffectSize + AOExMod, areaOfEffectSize + AOEzMod); //grabs the splat map data for our brush area
            for (int xx = 0; xx < areaOfEffectSize + AOEzMod; xx++)
            {
                for (int yy = 0; yy < areaOfEffectSize + AOExMod; yy++)
                {
                    float[] weights = new float[targetTerrain.terrainData.alphamapLayers]; //creates a float array and sets the size to be the number of paints your terrain has
                    for (int zz = 0; zz < splat.GetLength(2); zz++)
                    {
                        weights[zz] = splat[xx, yy, zz];//grabs the weights from the terrains splat map
                    }
                    weights[paint] += brush[xx - AOEzMod, yy - AOExMod] * strength * 2000; // adds weight to the paint currently selected with the int paint variable
                                                                                           //this next bit normalizes all the weights so that they will add up to 1
                    float sum = weights.Sum();
                    for (int ww = 0; ww < weights.Length; ww++)
                    {
                        weights[ww] /= sum;
                        splat[xx, yy, ww] = weights[ww];
                    }
                }
            }

            setAlphaMaps(x - AOExMod, z - AOEzMod, splat);

        }

    }

    public void setAlphaMaps(int mapX,int mapY,float[,,] splatMap){
        targetTerrain.terrainData.SetAlphamaps(mapX, mapY, splatMap);
        targetTerrain.Flush();
    }

    public void setCachedSplatMap()
    {
        CachedTerrainAlphamapData = targetTerrainData.GetAlphamaps(0, 0, targetTerrainData.alphamapWidth, targetTerrainData.alphamapHeight);
    }
    public int GetDominantTextureIndexAt(Vector3 worldPosition)
    {
      //  float[,,] CachedTerrainAlphamapData = splat;
        
        Vector3Int alphamapCoordinates = ConvertToAlphamapCoordinates(worldPosition);

        if (!CachedTerrainAlphamapData.ContainsIndex(alphamapCoordinates.x, dimension: 1))
            return -1;

        if (!CachedTerrainAlphamapData.ContainsIndex(alphamapCoordinates.z, dimension: 0))
            return -1;


        int mostDominantTextureIndex = 0;
        float greatestTextureWeight = float.MinValue;

        int textureCount = CachedTerrainAlphamapData.GetLength(2);
        for (int textureIndex = 0; textureIndex < textureCount; textureIndex++)
        {
            // I am really not sure why the x and z coordinates are out of order here, I think it's just Unity being lame and weird
            float textureWeight = CachedTerrainAlphamapData[alphamapCoordinates.z, alphamapCoordinates.x, textureIndex];

            if (textureWeight > greatestTextureWeight)
            {
                greatestTextureWeight = textureWeight;
                mostDominantTextureIndex = textureIndex;
            }
        }

        return mostDominantTextureIndex;


        Vector3Int ConvertToAlphamapCoordinates(Vector3 _worldPosition)
        {
            Vector3 relativePosition = _worldPosition - targetTerrain.transform.position;
            // Important note: terrains cannot be rotated, so we don't have to worry about rotation

            return new Vector3Int
            (
                x: Mathf.RoundToInt((relativePosition.x / targetTerrainData.size.x) * targetTerrainData.alphamapWidth),
                y: 0,
                z: Mathf.RoundToInt((relativePosition.z / targetTerrainData.size.z) * targetTerrainData.alphamapHeight)
            );
        }
    }

    void update()
    {

        if (GameController.t.currentCharacter== GameController.t.SPIDER)
        {
            draw();
        }

        if (GameController.t.currentCharacter == GameController.t.HERO)
        {
           GameController.t.currentTextureTerrain=GetDominantTextureIndexAt(GameController.t.HERO.position);
        }

        /* if (Input.GetMouseButton(0) && isDrawing==false)
         {
             isDrawing = true;
             InvokeRepeating("draw", 0, 0.05f);
         }

         if (!Input.GetMouseButton(0) && isDrawing==true)
         {
             isDrawing = false;
             CancelInvoke("draw");
         }*/
    }

    public void draw()
    {
        if (GameController.t.canPaintTerrain == false) return;
        if (currentBrush==1)
        {
            brush = brush2;
            currentBrush = 2;
        }
        else
        {
            brush = brush1;
            currentBrush = 1;
        }
        GetTerrainCoordinates(spider.transform.position, out int terX, out int terZ);
        ModifyTerrain(terX, terZ);

       /* RaycastHit hit;

        if (Physics.Raycast(spider.transform.position + new Vector3(0, 100, 0),
            -Vector3.up,
            out hit, 
            Mathf.Infinity,
            terrainLayerMask))
        {
            //targetTerrain = GetTerrainAtObject(hit.transform.gameObject);
            //---SetEditValues(targetTerrain);
            GetTerrainCoordinates(hit, out int terX, out int terZ);
            ModifyTerrain(terX, terZ);
        }*/
    }
}
[SerializeField]
public class LayerDatas{
    public float TilingSize = 1;
    public float normalScale = 1;

 }
