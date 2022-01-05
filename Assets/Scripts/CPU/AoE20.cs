using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AoE20 : MonoBehaviour
{
    Cell cell;
    public SimulationState state;
    public string input;
    public string[] lines;
    public int[] enchanceTable;
    public int[] image;

    public int width = 0;
    public int height = 0;

    public ComputeShader compute;
    public RenderTexture renderTexture;
    public ComputeBuffer bufferA, bufferB;
    public ComputeBuffer imageBuffer;
    public ComputeBuffer enchanceTableBuffer;
    public Material material;

    private int SetCellsKernel;
    private int ClearBuffersKernel;
    private int OneStepKernel;
    private bool pingPong;

    // Debug
    public Cell[] cellsADebug;
    public Cell[] cellsBDebug;
    //public ComputeBuffer debugBuffer;
    public int[] debugArray = new int[1];
    public ComputeBuffer counterBuffer;

    void Start () {
        if (height < 1 || width < 1) return;

        OneStepKernel = compute.FindKernel("OneStep");
        ClearBuffersKernel = compute.FindKernel("ClearBuffers");
        SetCellsKernel = compute.FindKernel("SetCells");

        AllocateMemory();
        ClearBuffers();
        LoadInput();

        enchanceTableBuffer = new ComputeBuffer(enchanceTable.Length, sizeof(int));
        enchanceTableBuffer.SetData(enchanceTable);
        imageBuffer = new ComputeBuffer(image.Length, sizeof(int));
        imageBuffer.SetData(image);

        pingPong = true;

        compute.SetInt("width", width);
        compute.SetInt("height", height);

        SetCells();
        //GetCells();
    }

    void AllocateMemory()
    {
        if (bufferA != null) bufferA.Release();
        if (bufferB != null) bufferB.Release();

        bufferA = new ComputeBuffer(width * height, sizeof(uint) * 3);
        bufferB = new ComputeBuffer(width * height, sizeof(uint) * 3);

        renderTexture = new RenderTexture(width, height, 24);
        renderTexture.wrapMode = TextureWrapMode.Repeat;
        renderTexture.enableRandomWrite = true;
        renderTexture.filterMode = FilterMode.Point;
        renderTexture.useMipMap = false;
        renderTexture.Create();

        //cellsADebug = new Cell[width * height];
        //cellsBDebug = new Cell[width * height];
        //debugBuffer = new ComputeBuffer(1, sizeof(uint));
        counterBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
    }

    void SetCells()
    {
        compute.SetTexture(SetCellsKernel, "Result", renderTexture);
        compute.SetBuffer(SetCellsKernel, "CellsA", bufferA);
        //compute.SetBuffer(SetCellsKernel, "CellsB", bufferB);
        compute.SetBuffer(SetCellsKernel, "enchanceTableBuffer", enchanceTableBuffer);
        compute.SetBuffer(SetCellsKernel, "imageBuffer", imageBuffer);
        compute.SetInt("imageWidth", state.width);
        compute.SetInt("imageHeight", state.height);
        //compute.SetBuffer(SetCellsKernel, "debugBuffer", debugBuffer);
        enchanceTableBuffer.SetData(enchanceTable);
        //compute.Dispatch(SetCellsKernel, 1, 1, 1);
        compute.DispatchThreads(SetCellsKernel, width, height, 1);
    }

    /*
    void GetCells()
    {
        bufferA.GetData(cellsADebug);
        bufferB.GetData(cellsBDebug);

        //debugBuffer.GetData(debugArray);
        
        Debug.Log(debugArray[0]);
    }
*/

    public void ClearBuffers()
    {
        compute.SetBuffer(ClearBuffersKernel, "CellsA", bufferA);
        compute.SetBuffer(ClearBuffersKernel, "CellsB", bufferB);
        counterBuffer.SetCounterValue(0);
        compute.SetBuffer(ClearBuffersKernel, "counterBuffer", counterBuffer);
        compute.DispatchThreads(ClearBuffersKernel, width, height, 1);
    }

    public void Step()
    {
        compute.SetTexture(OneStepKernel, "Result", renderTexture);

        if (true == pingPong)
        {
            compute.SetBuffer(OneStepKernel, "CellsA", bufferA);
            compute.SetBuffer(OneStepKernel, "CellsB", bufferB);
            pingPong = false;
        }
        else
        {
            compute.SetBuffer(OneStepKernel, "CellsA", bufferB);
            compute.SetBuffer(OneStepKernel, "CellsB", bufferA);
            pingPong = true;
        }

        compute.SetBuffer(OneStepKernel, "enchanceTableBuffer", enchanceTableBuffer);
        counterBuffer.SetCounterValue(0);
        compute.SetBuffer(OneStepKernel, "counterBuffer", counterBuffer);
        //compute.Dispatch(OneStepKernel, width / 8, height / 8, 1);
        compute.DispatchThreads(OneStepKernel, width, height, 1);
        counterBuffer.GetData(debugArray);

        material.mainTexture = renderTexture;
        //GetCells();
    }

    public void LoadInput()
    {
        // Loat input.txt
        input = System.IO.File.ReadAllText(@"C:\Users\Public\Documents\input20.txt");
        // Split input string into an array of strings, separated by empty lines and " "
        lines = input.Split(new string[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        // Split the first line into an array of integers (enchanceTable) where '.' is 0 and '#' is 1
        enchanceTable = new int[lines[0].Length];
        for (int i = 0; i < lines[0].Length; i++)
        {
            if (lines[0][i] == '.')
                enchanceTable[i] = 0;
            else
                enchanceTable[i] = 1;
        }

        // from line 2 to last line, split each line into an array of integers (image) where '.' is 0 and '#' is 1
        // add each integer to image array
        image = new int[5 * lines[4].Length];
        for (int i = 2; i < lines.Length; i++)
        {
            for (int j = 0; j < lines[i].Length; j++)
            {
                int index = (i - 2) * lines[i].Length + j;
                if (lines[i][j] == '.')
                    image[index] = 0;
                else
                    image[index] = 1;
            }
        }

        state = new SimulationState();
        state.cells = new Cell[image.Length];
        state.width = lines[5].Length;
        state.height = lines.Length - 2;
        for (int i = 0; i < image.Length; i++)
        {
            state.cells[i] = new Cell();
            state.cells[i].isAlive = (uint)image[i];
        }
    }

     void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(material.mainTexture, destination);
    }

    void OnDestroy()
    {
        bufferA?.Dispose();
        bufferB?.Dispose();
        imageBuffer?.Dispose();
        enchanceTableBuffer?.Dispose();

        renderTexture?.Release();

        counterBuffer?.Dispose();
        //debugBuffer?.Dispose();
    }

    // On mouse click save texture to .png file
    void OnGUI()
    {
        /*
        if (GUI.Button(new Rect(10, 10, 100, 30), "Save")
            & (renderTexture != null))
        {
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            byte[] bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(@"C:\Users\Public\Documents\output.png", bytes);
        }*/

        if (GUI.Button(new Rect(10, 90, 100, 30), "Step"))
        {
            Step();
        }

        // get counter value
        GUI.Label(new Rect(10, 50, 100, 30), debugArray[0].ToString());
    }    
}
