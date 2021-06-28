using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class MeshShaper : MonoBehaviour
{
    private MeshFilter mf;
    public Mesh originalMesh;
    private Mesh clonedMesh;
    public float offSetY = 0;


    private Vector3 currentPos;
    private Quaternion currentRot;
    private Vector3 currentScale;
    private float currentOffsetY;

    private MeshCollider mc;

    public LayerMask layer;

    void Start()
    {
        init();
    }

    public void init()
    {
        
        mf = GetComponent<MeshFilter>();
       
        if (originalMesh == null) originalMesh = mf.sharedMesh;

        if (originalMesh.isReadable == false)
        {
            Debug.Log(gameObject.name + " : MESH READ/WRITE ENABLED FALSE - CHANGE TO 'TRUE' IN INSPECTOR, AND RESET THIS COMPONENT");
            return;
        }

        mc = transform.gameObject.GetComponent<MeshCollider>();
        if (mc == null) mc = transform.gameObject.AddComponent<MeshCollider>();

        clonedMesh = new Mesh();
        clonedMesh.name = "clone_" + originalMesh.name;
        clonedMesh.vertices = originalMesh.vertices;
        clonedMesh.triangles = originalMesh.triangles;
        clonedMesh.normals = originalMesh.normals;
        clonedMesh.uv = originalMesh.uv;
        mf.mesh = clonedMesh;

        moveVertices();
        if (Application.isEditor == false) {
            gameObject.isStatic = true;
            StaticBatchingUtility.Combine(gameObject);
            originalMesh = null;
        } 
        
    }

    private void Update()
    {
        if (Application.isPlaying == true) return;

        if (currentPos != transform.position ||
            currentRot != transform.rotation ||
            currentScale != transform.localScale ||
            currentOffsetY!=offSetY)
        {
            moveVertices();
        }

        currentPos = transform.position;
        currentRot = transform.rotation;
        currentScale = transform.localScale;
        currentOffsetY = offSetY;

    }


    public void moveVertices()
    {
        if (originalMesh.vertices.Length!= clonedMesh.vertices.Length)
        {
            init();
            return;
        }
        Vector3[] _newVertices = new Vector3[originalMesh.vertices.Length];
        Vector3 _dir = -transform.up;

        int _inc = 0;
        foreach (Vector3 _vertice in originalMesh.vertices)
        {

            Vector3 _newPos = transform.TransformPoint(_vertice);

            RaycastHit hit;
            if (Physics.Raycast(_newPos, _dir, out hit, Mathf.Infinity, layer))
            {
                    
                _newPos = hit.point;
                _newPos += -_dir.normalized * _vertice.y*transform.localScale.y;
                _newPos += -_dir.normalized * offSetY* transform.localScale.y;
            }

            _newPos = transform.InverseTransformPoint(_newPos);
            _newVertices[_inc] = _newPos;
            _inc++;
        }

        clonedMesh.vertices = _newVertices;
        clonedMesh.RecalculateBounds();
        clonedMesh.normals = originalMesh.normals;
        mc.sharedMesh = clonedMesh;
    }


}
