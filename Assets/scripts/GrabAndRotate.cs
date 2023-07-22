using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SolidGeometryLib;
using System.Runtime.InteropServices;
using System;
using System.IO;
using UnityEditor;

public class GrabAndRotate : MonoBehaviour
{
    public GameObject leftGrabbedObject;
    public GameObject rightGrabbedObject;

    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;

    public float leftRotationAmplification;
    public float rightRotationAmplification;
    public float distMultiplier = 2.0f;


    // list of all game objects created
    private List<GameObject> gameObjects = new List<GameObject>();
    private List<IntPtr> intPtrs = new List<IntPtr>();
    private List<String> history = new List<String>();
    private sg_reconstruction reconstruction = new sg_reconstruction();

    private List<HashTreeNode> leftTrees = new List<HashTreeNode>();
    private Stack<HashTreeNode> leftTreeRightStack = new Stack<HashTreeNode>();

    private List<IntPtr> leftIntPtrs = new List<IntPtr>();
    private Stack<IntPtr> leftIntPtrsRightStack = new Stack<IntPtr>();

    private HashTreeNode currentLeft = null;
    private HashTreeNode currentRight = null;
    private HashTreeNode tree = null;

    private IntPtr currentLeftIntPtr = IntPtr.Zero;
    private IntPtr currentRightIntPtr = IntPtr.Zero;
    private IntPtr treeIntPtr = IntPtr.Zero;

    private HashTree currentHashTree = new HashTree();

    private bool isGrabbing = false;
    private bool isHoldingLeft = false;
    private bool isHoldingRight = false;
    private float timeSinceLastSculpt = 0.0f;

    private Vector3 lastLeftHandPosition = new Vector3(0.0f, 0.0f, 2.0f);
    private Vector3 lastLeftPalmPosition = Vector3.zero;
    private Quaternion lastLeftHandRotation = Quaternion.identity;

    private Vector3 lastRightHandPosition = new Vector3(0.0f, 0.0f, 2.0f);
    private Vector3 lastRightPalmPosition = Vector3.zero;
    private Quaternion lastRightHandRotation = Quaternion.identity;

    private bool sgStarted = false;
    private bool sgStopped = false;

    private bool isAddMode = false;

    private bool brushIsSmallCube = true;
    private string pointePath = @"C:\Users\edegu\vr_sculpter\Assets\tools\pointe.txt";
    private string smallCubePath = @"C:\Users\edegu\sculptures\small_cube_2023_07_22_12_22_17.txt";

    void Start()
    {
        if(!sgStarted) {
            Debug.Log("sgstart Start");
            // do a try catch for debug log

            try {
                SolidGeometryLibIntegration.sg_core_start();
                Debug.Log("started");
            } catch (Exception e) {
                Debug.Log("start Exception: " + e.ToString());
            } finally {
                Debug.Log("finally");
            }
            sgStarted = true;
        }

        LoadBrush(pointePath);
        brushIsSmallCube = true;
    }
        

    void OnDestroy()
    {   
        Debug.Log("OnDestroy");
        if(sgStarted && !sgStopped) {
            Debug.Log("sgstop OnDestroy");
            Debug.Log("gameObjects.Count: " + gameObjects.Count.ToString());

            var alreadyFreed = new HashSet<IntPtr>();

            for(int i = gameObjects.Count - 1; i >= 0; i--) {
                
                if(gameObjects[i] == null) {
                    Debug.Log("gameObjects[" + i.ToString() + "] is null");
                    continue;
                }

                if(gameObjects[i].GetComponent<sgObject>() != null) {
                    IntPtr ptr = gameObjects[i].GetComponent<sgObject>().GetHandle();
                    alreadyFreed.Add(ptr);
                }
                DestroyImmediate(gameObjects[i]);
                Debug.Log("destroyed gameObjects " + i.ToString());
            }
            // keep track of already freed pointers to avoid double free
            

            for(int i = intPtrs.Count - 1; i >= 0; i--) {
                if(intPtrs[i] == IntPtr.Zero) {
                    Debug.Log("intPtrs[" + i.ToString() + "] is IntPtr.Zero");
                    continue;
                }
                if(alreadyFreed.Contains(intPtrs[i])) {
                    Debug.Log("intPtrs[" + i.ToString() + "] already freed");
                    continue;
                }
                SolidGeometryLibIntegration.sg_object_free(intPtrs[i]);
                alreadyFreed.Add(intPtrs[i]);
                Debug.Log("freed intPtrs " + i.ToString());
            }
            try {
                SolidGeometryLibIntegration.sg_core_stop();
                Debug.Log("stopped");
            } catch (Exception e) {
                Debug.Log("stop Exception: " + e.ToString());
            } finally {
                Debug.Log("finally");
            }
            sgStopped = true;
        }
        
    }


    void handleHand(OVRInput.Controller controller, GameObject grabbedObject, bool isLeft)
    {
        if(isLeft && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) < 0.1f) {
            lastLeftPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastLeftHandRotation = OVRInput.GetLocalControllerRotation(controller);
            return;
        }
        if(isLeft && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) >= 0.1f) {
            grabbedObject.transform.position = lastLeftHandPosition + distMultiplier * ( OVRInput.GetLocalControllerPosition(controller) - lastLeftPalmPosition);

            Quaternion changeInRotation = OVRInput.GetLocalControllerRotation(controller) * Quaternion.Inverse(lastLeftHandRotation);
            grabbedObject.transform.rotation = changeInRotation * grabbedObject.transform.rotation;

            lastLeftPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastLeftHandPosition = grabbedObject.transform.position;
            lastLeftHandRotation = OVRInput.GetLocalControllerRotation(controller);
        }

        if(!isLeft && OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) < 0.1f) {
            lastRightPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastRightHandRotation = OVRInput.GetLocalControllerRotation(controller);
            return;
        }
        if(!isLeft &&  OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) >= 0.1f) {
            Debug.Log("Grabbing with right hand");
            grabbedObject.transform.position = lastRightHandPosition + distMultiplier * ( OVRInput.GetLocalControllerPosition(controller) - lastRightPalmPosition);

            Quaternion changeInRotation = OVRInput.GetLocalControllerRotation(controller) * Quaternion.Inverse(lastRightHandRotation);
            grabbedObject.transform.rotation = changeInRotation * grabbedObject.transform.rotation;

            lastRightPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastRightHandPosition = grabbedObject.transform.position;
            lastRightHandRotation = OVRInput.GetLocalControllerRotation(controller);
        }
    }


    IntPtr boolean_sub(IntPtr firstSelected, IntPtr secondSelected) {
        IntPtr res = IntPtr.Zero;
        IntPtr subResGroup = SolidGeometryLibIntegration.sg_bool_sub(firstSelected, secondSelected);

            if (subResGroup != IntPtr.Zero)
            {
                if (SolidGeometryLibIntegration.sg_object_type(subResGroup) == 10)   //  10 - type of group
                {
                    int chCnt = SolidGeometryLibIntegration.sg_group_child_count(subResGroup);
                    Debug.Log("subResGroup has " + chCnt.ToString() + " children");
                    // just get the first group
                    for (int i = 0; i < chCnt && i < 1; i++)
                    {
                        IntPtr curCh = SolidGeometryLibIntegration.sg_group_child(subResGroup, i);
                        res = curCh;
                    }
                    SolidGeometryLibIntegration.sg_group_break(subResGroup);
                    intPtrs.Add(subResGroup);
                } else {
                    Debug.Log("subResGroup is not a group");
                }
            } else {
                Debug.Log("subResGroup is null");
            }
            return res;
    }

    IntPtr boolean_add(IntPtr firstSelected, IntPtr secondSelected) {
        IntPtr res = IntPtr.Zero;
        IntPtr unionResGroup = SolidGeometryLibIntegration.sg_bool_union(firstSelected, secondSelected);

            if (unionResGroup != IntPtr.Zero)
            {
                if (SolidGeometryLibIntegration.sg_object_type(unionResGroup) == 10)   //  10 - type of group
                {
                    int chCnt = SolidGeometryLibIntegration.sg_group_child_count(unionResGroup);
                    for (int i = 0; i < chCnt && i < 1; i++)
                    {
                        IntPtr curCh = SolidGeometryLibIntegration.sg_group_child(unionResGroup, i);
                        res = curCh;
                    }
                    SolidGeometryLibIntegration.sg_group_break(unionResGroup);
                    intPtrs.Add(unionResGroup);
                }
            }
            return res;
    }

    void handlePinch(GameObject grabbedObject)
    {
        timeSinceLastSculpt += Time.deltaTime;

        if  (OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) >= 0.1f && !isGrabbing && timeSinceLastSculpt > 0.5f)
        {
            isGrabbing = true;
            // indicate of operation + random number
            Debug.Log("Grabbing, perform sculpting " + UnityEngine.Random.Range(0, 1000).ToString());
            //debugPosition(rightGrabbedObject);
            //test();
            debugPositionWithLeft(rightGrabbedObject);
            //testAllocationDealloation();
            //testIntPtrResultShared();
            timeSinceLastSculpt = 0.0f;
        }
        else if (OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) < 0.1f && isGrabbing)
        {
            isGrabbing = false;
        }
    }

    void debugPosition(GameObject obrush)
    {
        GameObject brush = Instantiate(obrush);
        Debug.Log("Position: " + brush.transform.position);
        
        if(brush.GetComponent<MeshFilter>() != null) {
           DestroyImmediate(brush.GetComponent<MeshFilter>());
        }
        
        if(brush.GetComponent<MeshRenderer>() != null) {
           DestroyImmediate(brush.GetComponent<MeshRenderer>());
        }
        Debug.Log("Removed meshFilter and meshRenderer");

        sgObject brushSgObject = brush.AddComponent<sgObject>();
        if(brushSgObject == null) {
            Debug.Log("brushSgObject is null");
            return;
        }

        Debug.Log("Added sgObject");

        // Allocate cubes to brushSgObject and sculptureSgObject
        IntPtr brushCube = SolidGeometryLibIntegration.sg_object_box(brush.transform.localScale.x, 
            brush.transform.localScale.y, brush.transform.localScale.z);

        Debug.Log("Allocated brushCube");

        Vector3 newPositions = new Vector3(brush.transform.position.x - brush.transform.localScale.x / 2.0f,
            brush.transform.position.y - brush.transform.localScale.y / 2.0f,
            brush.transform.position.z - brush.transform.localScale.z / 2.0f);

        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center

        //sgi.sg_object_rotate(brushCube.GetHandle(), brush.transform.rotation.x, brush.transform.rotation.y, brush.transform.rotation.z, brush.transform.rotation.w);
        
        
        Quaternion rotationBrush = brush.transform.rotation;
        rotationBrush.ToAngleAxis(out float angleDegBrush, out Vector3 axisBrush);
        float angleRadBrush = angleDegBrush * Mathf.Deg2Rad;

        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        
        Debug.Log("Moved brushCube");

        brushSgObject.InitObject(brushCube);

        Debug.Log("Initialized brushSgObject");
        

        GameObject materializedSculpture = new GameObject();

        moveSgObjectInverse(brush, brushCube);
        materializedSculpture.AddComponent<sgObject>().InitObject(brushCube);

        Debug.Log("Materialized Position: " + materializedSculpture.transform.position);
        Debug.Log("Materialized Rotation: " + materializedSculpture.transform.rotation);

        Destroy(brush);

        Debug.Log("Finished debugPosition");

        gameObjects.Add(materializedSculpture);

    }

    Vector3 getNewPosition(GameObject brush) {
        return new Vector3(brush.transform.position.x - brush.transform.localScale.x / 2.0f,
            brush.transform.position.y - brush.transform.localScale.y / 2.0f,
            brush.transform.position.z - brush.transform.localScale.z / 2.0f);
    }

    Vector3 getNewPositionNoOffset(GameObject brush) {
        return new Vector3(brush.transform.position.x,
            brush.transform.position.y,
            brush.transform.position.z);
    }

    Vector3 getRotationAxis(GameObject brush) {
        Quaternion rotationBrush = brush.transform.rotation;
        rotationBrush.ToAngleAxis(out float angleDegBrush, out Vector3 axisBrush);
        float angleRadBrush = angleDegBrush * Mathf.Deg2Rad;
        return axisBrush;
    }

    float getRotationAngleRad(GameObject brush) {
        Quaternion rotationBrush = brush.transform.rotation;
        rotationBrush.ToAngleAxis(out float angleDegBrush, out Vector3 axisBrush);
        float angleRadBrush = angleDegBrush * Mathf.Deg2Rad;
        return angleRadBrush;
    }

    IntPtr GetExistingIntPtrObject(GameObject brush) {
        IntPtr brushCube = brush.GetComponent<sgObject>().GetHandle();

        Vector3 newPositions = getNewPosition(brush);
        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center

        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);

        return brushCube;
    }

    sgObject GetSgObject(GameObject obj) {
        GameObject brush = Instantiate(obj);
        Debug.Log("Position: " + brush.transform.position);
        
        if(brush.GetComponent<MeshFilter>() != null) {
           DestroyImmediate(brush.GetComponent<MeshFilter>());
        }
        
        if(brush.GetComponent<MeshRenderer>() != null) {
           DestroyImmediate(brush.GetComponent<MeshRenderer>());
        }
        Debug.Log("Removed meshFilter and meshRenderer");

        sgObject brushSgObject = brush.AddComponent<sgObject>();
        if(brushSgObject == null) {
            Debug.Log("brushSgObject is null");
            return null;
        }

        Debug.Log("Added sgObject");

        // Allocate cubes to brushSgObject and sculptureSgObject
        IntPtr brushCube = SolidGeometryLibIntegration.sg_object_box(brush.transform.localScale.x, 
            brush.transform.localScale.y, brush.transform.localScale.z);

        Debug.Log("Allocated brushCube");

        Vector3 newPositions = getNewPosition(brush);

        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center


        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        
        Debug.Log("Moved brushCube");

        brushSgObject.InitObject(brushCube);

        Debug.Log("Initialized brushSgObject");

        Destroy(brush);
        return brushSgObject;
    }

    void writeHistory(String s) {
        history.Add(s);
    }

    HashTreeNode AlignTool(GameObject brush, HashTreeNode boxRoot) {
        Vector3 newPositions = getNewPosition(brush);

        boxRoot = sg_export.move(boxRoot, currentHashTree, newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center

        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        boxRoot = sg_export.rotate(boxRoot, currentHashTree, brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);

        return boxRoot;
    }

    void InitGOWithBasicBox(GameObject brush, bool isLeftTree) {
        sgObject brushSgObject = brush.AddComponent<sgObject>();

        // Allocate cubes to brushSgObject and sculptureSgObject
        IntPtr brushCube = SolidGeometryLibIntegration.sg_object_box(brush.transform.localScale.x, 
            brush.transform.localScale.y, brush.transform.localScale.z);

        HashTreeNode boxRoot = sg_export.box(brush.transform.localScale.x, 
            brush.transform.localScale.y, brush.transform.localScale.z, currentHashTree);
        

        Vector3 newPositions = getNewPosition(brush);

        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        boxRoot = sg_export.move(boxRoot, currentHashTree, newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center

        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        boxRoot = sg_export.rotate(boxRoot, currentHashTree, brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);

        // remove mesh filter and mesh renderer
        if(brush.GetComponent<MeshFilter>() != null) {
           DestroyImmediate(brush.GetComponent<MeshFilter>());
        }

        if(brush.GetComponent<MeshRenderer>() != null) {
           DestroyImmediate(brush.GetComponent<MeshRenderer>());
        }


        brushSgObject.InitObject(brushCube);
        if(isLeftTree) {
            currentLeft = boxRoot;
            currentLeftIntPtr = brushCube;
        } else {
            currentRight = boxRoot;
            currentRightIntPtr = brushCube;
        }
    }

    GameObject GetGOObjectClean(GameObject obj, bool isLeftTree) {
        GameObject brush = obj;
        GameObject brushEmpty = new GameObject("brush / sculpt Empty");
        sgObject brushSgObject = brushEmpty.AddComponent<sgObject>();

        // Allocate cubes to brushSgObject and sculptureSgObject
        IntPtr brushCube = SolidGeometryLibIntegration.sg_object_box(brush.transform.localScale.x, 
            brush.transform.localScale.y, brush.transform.localScale.z);

        HashTreeNode boxRoot = sg_export.box(brush.transform.localScale.x, 
            brush.transform.localScale.y, brush.transform.localScale.z, currentHashTree);
        

        Vector3 newPositions = getNewPosition(brush);

        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        boxRoot = sg_export.move(boxRoot, currentHashTree, newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center

        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        boxRoot = sg_export.rotate(boxRoot, currentHashTree, brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);

        brushSgObject.InitObject(brushCube);
        if(isLeftTree) {
            currentLeft = boxRoot;
            currentLeftIntPtr = brushCube;
        } else {
            currentRight = boxRoot;
            currentRightIntPtr = brushCube;
        }

        return brushEmpty;
    }

    sgObject GetExistingSgObject(GameObject obj) {
        GameObject brush = obj;
        // Allocate cubes to brushSgObject and sculptureSgObject
        IntPtr brushCube = brush.GetComponent<sgObject>().GetHandle();

        Debug.Log("Allocated brushCube");

        Vector3 newPositions = getNewPositionNoOffset(brush);

        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center


        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        
        Debug.Log("Moved brushCube");

        GameObject emtpy = new GameObject();
        sgObject brushSgObject = emtpy.AddComponent<sgObject>();

        brushSgObject.InitObject(brushCube);

        Debug.Log("Initialized brushSgObject");
        // Hide empty Renderer
        Renderer rend = emtpy.GetComponent<Renderer>();
        rend.enabled = false;


        return brushSgObject;
    }

    IntPtr GetExistingIntPtrClean(GameObject obj, bool isLeftTree) {
        GameObject brush = obj;
        // Allocate cubes to brushSgObject and sculptureSgObject
        IntPtr brushCube = brush.GetComponent<sgObject>().GetHandle();
        Vector3 newPositions = getNewPositionNoOffset(brush);
        HashTreeNode currentBox = null;
        if(isLeftTree) {
            currentBox = currentLeft;
        } else {
            currentBox = currentRight;
        }

        SolidGeometryLibIntegration.sg_object_move(brushCube, 
            newPositions.x, newPositions.y, newPositions.z);
        // looks like sgObject uses the corner as the center but Unity uses the center as the center
        currentBox = sg_export.move(currentBox, currentHashTree, newPositions.x, newPositions.y, newPositions.z);


        Vector3 axisBrush = getRotationAxis(brush);
        float angleRadBrush = getRotationAngleRad(brush);
        // You would then call the function with this data:
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        currentBox = sg_export.rotate(currentBox, currentHashTree, brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        
        if(isLeftTree) {
            currentLeft = currentBox;
            currentLeftIntPtr = brushCube;
        } else {
            currentRight = currentBox;
            currentRightIntPtr = brushCube;
        }
        
        return brushCube;
    }

    void test() {
        DestroyImmediate(null);
    }

    void moveSgObjectInverse(GameObject original, IntPtr obj) {
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);

        /* original rotation
        SolidGeometryLibIntegration.sg_object_rotate(brushCube, 
            brush.transform.position.x, brush.transform.position.y, brush.transform.position.z,
            axisBrush.x, axisBrush.y, axisBrush.z, angleRadBrush);
        */
        SolidGeometryLibIntegration.sg_object_rotate(obj,
            original.transform.position.x, original.transform.position.y, original.transform.position.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, -rotationAngleRad);

        SolidGeometryLibIntegration.sg_object_move(obj, 
            -newPosition.x - original.transform.localScale.x / 2.0f, 
            -newPosition.y - original.transform.localScale.y / 2.0f,
            -newPosition.z - original.transform.localScale.z / 2.0f);
    }

    HashTreeNode moveTreeInverse(GameObject original, HashTreeNode node) {
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);

        node = sg_export.rotate(node, currentHashTree, original.transform.position.x, original.transform.position.y, original.transform.position.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, -rotationAngleRad);
        node = sg_export.move(node, currentHashTree, -newPosition.x - original.transform.localScale.x / 2.0f, 
            -newPosition.y - original.transform.localScale.y / 2.0f,
            -newPosition.z - original.transform.localScale.z / 2.0f);
        return node;
    }

    IntPtr moveIntPtrInverse(GameObject original, IntPtr ptr) {
        // warning this will move the stuff
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);

        SolidGeometryLibIntegration.sg_object_rotate(ptr,
            original.transform.position.x, original.transform.position.y, original.transform.position.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, -rotationAngleRad);

        SolidGeometryLibIntegration.sg_object_move(ptr, 
            -newPosition.x - original.transform.localScale.x / 2.0f, 
            -newPosition.y - original.transform.localScale.y / 2.0f,
            -newPosition.z - original.transform.localScale.z / 2.0f);
        return ptr;
    }

    void testIntPtrShared() {
        /*
        Scenarios:
         1- the intptr is shared
         2- the intptr is not shared
        */

        // test intptr shared
        GameObject test = new GameObject();
        GameObject test2 = new GameObject();

        // Remove MeshFilter and MeshRenderer from test and test2
        DestroyImmediate(test.GetComponent<MeshFilter>());
        DestroyImmediate(test.GetComponent<MeshRenderer>());
        DestroyImmediate(test2.GetComponent<MeshFilter>());
        DestroyImmediate(test2.GetComponent<MeshRenderer>());

        sgObject sg = test.AddComponent<sgObject>();
        sgObject sg2 = test2.AddComponent<sgObject>();

        IntPtr intptr = SolidGeometryLibIntegration.sg_object_box(1.0f, 1.0f, 1.0f);
        
        sg.InitObject(intptr);
        sg2.InitObject(intptr);

        DestroyImmediate(test);
        DestroyImmediate(test2); // Crashes
        // This test proves the IntPtr are shared
    }

    void testIntPtrResultShared() {

        GameObject test = new GameObject();
        GameObject test2 = new GameObject();

        // Remove MeshFilter and MeshRenderer from test and test2
        DestroyImmediate(test.GetComponent<MeshFilter>());
        DestroyImmediate(test.GetComponent<MeshRenderer>());
        DestroyImmediate(test2.GetComponent<MeshFilter>());
        DestroyImmediate(test2.GetComponent<MeshRenderer>());

        sgObject sg = test.AddComponent<sgObject>();
        sgObject sg2 = test2.AddComponent<sgObject>();

        IntPtr cube1 = SolidGeometryLibIntegration.sg_object_box(1.0f, 1.0f, 1.0f);
        IntPtr cube2 = SolidGeometryLibIntegration.sg_object_sphere(1.0f, 10, 10);

        sg.InitObject(cube1);
        sg2.InitObject(cube2);

        GameObject result = new GameObject();

        IntPtr resultIntPtr = boolean_sub(cube1, cube2);
        result.AddComponent<sgObject>().InitObject(resultIntPtr);


        //DestroyImmediate(result); // works, more stable
        DestroyImmediate(test);
        DestroyImmediate(test2);
        DestroyImmediate(result); // unstable
        

    }

    void debugPositionWithLeft(GameObject obrush)
    {
        if(leftGrabbedObject == null) {
            Debug.Log("leftGrabbedObject is null");
            return;
        }

        if(obrush == null) {
            Debug.Log("obrush is null");
            return;
        }
        IntPtr sculptedIntPtr = IntPtr.Zero;
        IntPtr sculptureIntPtr = IntPtr.Zero;;
        IntPtr brushIntPtr = IntPtr.Zero;

        GameObject brushEmpty = null;
        GameObject sculptureEmpty = null;
        if(leftGrabbedObject.GetComponent<sgObject>() == null) {
            if(obrush.GetComponent<sgObject>() != null) {
                brushIntPtr = GetExistingIntPtrClean(obrush, false);
                Debug.Log("Loaded brushIntPtr");
            } else {
                // TODO avoid recreating the brush
                brushEmpty = GetGOObjectClean(obrush, false);
                Debug.Log("Sculpting with box");
            }
            
            sculptureEmpty = GetGOObjectClean(leftGrabbedObject, true);

            gameObjects.Add(brushEmpty);
            gameObjects.Add(sculptureEmpty);
            
            leftTrees.Add(moveTreeInverse(leftGrabbedObject, currentLeft));
           

            Debug.Log("Doing first sculpture");
            if(brushIntPtr != IntPtr.Zero) {
                if(isAddMode) {
                    sculptedIntPtr = boolean_add(sculptureEmpty.GetComponent<sgObject>().GetHandle(), brushIntPtr);
                } else {
                    sculptedIntPtr = boolean_sub(sculptureEmpty.GetComponent<sgObject>().GetHandle(), brushIntPtr);
                }
                
            } else {
                if(isAddMode) {
                    sculptedIntPtr = boolean_add(sculptureEmpty.GetComponent<sgObject>().GetHandle(), brushEmpty.GetComponent<sgObject>().GetHandle());
                } else {
                    sculptedIntPtr = boolean_sub(sculptureEmpty.GetComponent<sgObject>().GetHandle(), brushEmpty.GetComponent<sgObject>().GetHandle());
                }
                
            }
        } else {
            leftTrees.Add(currentLeft);
            leftIntPtrs.Add(currentLeftIntPtr);
           

            if(obrush.GetComponent<sgObject>() != null) {
                brushIntPtr = GetExistingIntPtrClean(obrush, false);
                Debug.Log("Loaded brushIntPtr");
            } else {
                brushEmpty = GetGOObjectClean(obrush, false);
                Debug.Log("Sculpting with box");
            }
            
            sculptureIntPtr = GetExistingIntPtrClean(leftGrabbedObject, true);
            

            gameObjects.Add(brushEmpty);
            
            Debug.Log("Doing subsequent sculpture");
            if(brushIntPtr != IntPtr.Zero) {
                if(isAddMode) {
                    sculptedIntPtr = boolean_add(sculptureIntPtr, brushIntPtr);

                } else {
                    sculptedIntPtr = boolean_sub(sculptureIntPtr, brushIntPtr);
                }
                
            } else {
                if(isAddMode) {
                    sculptedIntPtr = boolean_add(sculptureIntPtr, brushEmpty.GetComponent<sgObject>().GetHandle());
                } else {
                    sculptedIntPtr = boolean_sub(sculptureIntPtr, brushEmpty.GetComponent<sgObject>().GetHandle());
                }
                
            }
        }

        intPtrs.Add(sculptureIntPtr);
        intPtrs.Add(brushIntPtr);
        intPtrs.Add(sculptedIntPtr);


        if(sculptedIntPtr == IntPtr.Zero) {
            Debug.Log("sculptedSgObject is null");
            currentLeft = moveTreeInverse(leftGrabbedObject, currentLeft);
            currentRight = moveTreeInverse(obrush, currentRight);
            if (sculptureIntPtr != IntPtr.Zero) {
                moveSgObjectInverse(leftGrabbedObject, sculptureIntPtr);
                currentLeftIntPtr = sculptureIntPtr;
            }
            if (brushIntPtr != IntPtr.Zero) {
                moveSgObjectInverse(obrush, brushIntPtr);
                currentRightIntPtr = brushIntPtr;
            }
            HideGameObject(brushEmpty);
            HideGameObject(sculptureEmpty);
            return;
        } else {
            if(isAddMode) {
                tree = sg_export.add(currentLeft, currentRight, currentHashTree);
            } else {
                tree = sg_export.sub(currentLeft, currentRight, currentHashTree);
            }
            
            
            GameObject sculpture = new GameObject("Sculpture");
            sculpture.transform.position = new Vector3(0, 0, 0);
            sculpture.transform.rotation = new Quaternion(0, 0, 0, 0);

            moveSgObjectInverse(leftGrabbedObject, sculptedIntPtr);
            currentLeftIntPtr = sculptedIntPtr;

            currentRight = moveTreeInverse(obrush, currentRight);
            tree = moveTreeInverse(leftGrabbedObject, tree);
            currentLeft = tree;

            if (brushIntPtr != IntPtr.Zero) {
                moveSgObjectInverse(obrush, brushIntPtr);
                currentRightIntPtr = brushIntPtr;
            }
            
            sgObject sculptureSgObject = sculpture.AddComponent<sgObject>();
            sculptureSgObject.InitObject(sculptedIntPtr);
            BoxCollider[] boxColliders = sculpture.GetComponents<BoxCollider>();
            for(int i = 0; i < boxColliders.Length; i++) {
                DestroyImmediate(boxColliders[i]);
            }

            RenameObject(leftGrabbedObject, "oldSculpture");
            HideGameObject(leftGrabbedObject);
            HideGameObject(sculptureEmpty);


            sculpture.transform.position = leftGrabbedObject.transform.position;
            sculpture.transform.rotation = leftGrabbedObject.transform.rotation;

            gameObjects.Add(sculpture);
            leftGrabbedObject = sculpture;
            Debug.Log("Finished sculpting " + UnityEngine.Random.Range(0, 1000).ToString());


            // Do the same with the brush

            if(brushEmpty != null) {

                GameObject newBrush = new GameObject("NewBrush");
                newBrush.transform.position = new Vector3(0, 0, 0);
                newBrush.transform.rotation = new Quaternion(0, 0, 0, 0);
                brushIntPtr = GetExistingIntPtrClean(brushEmpty, false);
                moveSgObjectInverse(obrush, brushIntPtr);

                newBrush.AddComponent<sgObject>().InitObject(brushIntPtr);

                newBrush.transform.position = obrush.transform.position;
                newBrush.transform.rotation = obrush.transform.rotation;
                HideGameObject(brushEmpty);
                HideGameObject(rightGrabbedObject);
                rightGrabbedObject = newBrush;
                gameObjects.Add(newBrush);
                gameObjects.Add(rightGrabbedObject);

            }

            // Flash a message to the user
            GameObject text = new GameObject("Text");
            TextMesh textMesh = text.AddComponent<TextMesh>();
            textMesh.text = "Sculpted";
            textMesh.fontSize = 100;
            textMesh.color = Color.yellow;
            text.transform.position = new Vector3(0, 0, 0);
            text.transform.rotation = new Quaternion(0, 0, 0, 0);
            text.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            gameObjects.Add(text);
            StartCoroutine(DestroyAfterxSeconds(0.3f, text));

        }
    }

     IEnumerator DestroyAfterxSeconds(float x, GameObject obj)
    {
        yield return new WaitForSeconds(x);
        Destroy(obj);
    }

    void Undo() {
        if(leftTrees.Count == 0) {
            Debug.Log("No more undo");
            return;
        }
        Debug.Log("Undoing");
        HashTreeNode lastTree = leftTrees[leftTrees.Count - 1];
        leftTrees.RemoveAt(leftTrees.Count - 1);
        HideGameObject(leftGrabbedObject);
        
        
        GameObject newSculpture = reconstruction.ReconstructFromTree(lastTree, currentHashTree);
        newSculpture.transform.position = leftGrabbedObject.transform.position;
        newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;
        leftGrabbedObject = newSculpture;

        gameObjects.Add(leftGrabbedObject);
        leftTreeRightStack.Push(currentLeft);
        currentLeft = lastTree;
    }

    void UndoIntPtr() {
        if(leftTrees.Count == 0) {
            Debug.Log("No more undo");
            return;
        }
        Debug.Log("Undoing");
        HashTreeNode lastTree = leftTrees[leftTrees.Count - 1];
        leftTrees.RemoveAt(leftTrees.Count - 1);
        leftTreeRightStack.Push(currentLeft);
        currentLeft = lastTree;

        if(leftIntPtrs.Count == 0) {
            Debug.Log("No more undo");
            return;
        }
        Debug.Log("Undoing");
        IntPtr lastIntPtr = leftIntPtrs[leftIntPtrs.Count - 1];
        leftIntPtrs.RemoveAt(leftIntPtrs.Count - 1);

        HideGameObject(leftGrabbedObject);
        
        
        GameObject newSculpture = new GameObject("Sculpture");
        moveSgObjectInverse(leftGrabbedObject, lastIntPtr);
        newSculpture.AddComponent<sgObject>().InitObject(lastIntPtr);
        
        newSculpture.transform.position = leftGrabbedObject.transform.position;
        newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;
        
        leftGrabbedObject = newSculpture;

        gameObjects.Add(leftGrabbedObject);
        leftIntPtrsRightStack.Push(currentLeftIntPtr);
        currentLeftIntPtr = lastIntPtr;
    }

    void Redo() {
        if(leftTreeRightStack.Count == 0) {
            Debug.Log("No more redo");
            return;
        }
        Debug.Log("Redoing");
        HashTreeNode lastTree = leftTreeRightStack.Pop();
        HideGameObject(leftGrabbedObject);
        GameObject newSculpture = reconstruction.ReconstructFromTree(lastTree, currentHashTree);
        newSculpture.transform.position = leftGrabbedObject.transform.position;
        newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;
        leftGrabbedObject = newSculpture;
        gameObjects.Add(leftGrabbedObject);
        leftTrees.Add(currentLeft);
        currentLeft = lastTree;
    }

    void RedoIntPtr() {
        if(leftTreeRightStack.Count == 0) {
            Debug.Log("No more redo");
            return;
        }
        Debug.Log("Redoing");
        HashTreeNode lastTree = leftTreeRightStack.Pop();
        leftTrees.Add(currentLeft);
        currentLeft = lastTree;

        if(leftIntPtrsRightStack.Count == 0) {
            Debug.Log("No more redo");
            return;
        }
        Debug.Log("Redoing");
        IntPtr lastIntPtr = leftIntPtrsRightStack.Pop();
        HideGameObject(leftGrabbedObject);

        GameObject newSculpture = new GameObject("Sculpture");
        newSculpture.AddComponent<sgObject>().InitObject(lastIntPtr);
        newSculpture.transform.position = leftGrabbedObject.transform.position;
        newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;
        
        leftGrabbedObject = newSculpture;

        gameObjects.Add(leftGrabbedObject);

        leftIntPtrs.Add(currentLeftIntPtr);
        currentLeftIntPtr = lastIntPtr;
    }

    void RenameObject(GameObject obj, string newName) {
        obj.name = newName;
    }

    void HideGameObject(GameObject obj) {
        if (obj != null ) {
            Renderer rend = obj.GetComponent<Renderer>();
            rend.enabled = false;
        }
        
    }

    void EmptyUpdate() {

    }



    void Update()
    {
        //handleHandKeyboard(rightHand, leftGrabbedObject, true);
        //handleHandKeyboard(rightHand, rightGrabbedObject, false);

        handleHand(leftController, leftGrabbedObject, true);
        handleHand(rightController, rightGrabbedObject, false);

        handlePinch(leftGrabbedObject);

        if(Input.GetKeyDown(KeyCode.S)) {
            string path = EditorUtility.SaveFilePanel("Save file as", "Assets/tools", "MyCoolSculptingTool", "txt");
            Debug.Log("Selected file: " + path);
            sg_export.writeToFile(currentLeft, currentHashTree, path);
        }

        if(Input.GetKeyDown(KeyCode.Space)) {
            isHoldingLeft = !isHoldingLeft;
        }

        if(Input.GetKeyDown(KeyCode.C)) {
            isHoldingRight = !isHoldingRight;
        }

        if(Input.GetKeyDown(KeyCode.Z)) {
            UndoIntPtr();
        }

        if(Input.GetKeyDown(KeyCode.X)) {
            RedoIntPtr();
        }

        if(OVRInput.GetDown(OVRInput.Button.One)) {
            UndoIntPtr();
        }

        if(OVRInput.GetDown(OVRInput.Button.Two)) {
            RedoIntPtr();
        }

        if(OVRInput.GetDown(OVRInput.Button.Three)) {
            // create a file called YYYY_MM_DD_HH_MM_SS.txt
            string path = @"C:\Users\edegu\sculptures\" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt";

            sg_export.writeToFile(currentLeft, currentHashTree, path);
            obj_exporter.MeshToFile(leftGrabbedObject.GetComponent<MeshFilter>() , "Assets", "obj_export_" +  DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".obj");

        }

        if(OVRInput.GetDown(OVRInput.Button.Start)) {
            isAddMode = !isAddMode;
        }

        if(OVRInput.GetDown(OVRInput.Button.Four)) {
            if(brushIsSmallCube) {
                brushIsSmallCube = false;
                LoadBrush(pointePath);
            } else {
                brushIsSmallCube = true;
                LoadBrush(smallCubePath);
            }
        }

        if(Input.GetKeyDown(KeyCode.R)) {
            string path = EditorUtility.OpenFilePanel("Open file", "Assets/tools", "txt");
            Debug.Log("Selected file: " + path);
            LoadBrush(path);
        }

        if(Input.GetKeyDown(KeyCode.L)) {
            string path = EditorUtility.OpenFilePanel("Open file", "Assets/tools", "txt");
            Debug.Log("Selected file: " + path);
            LoadSculpture(path);
        }
    }

    void LoadSculpture(string path) {
        HashTree resTree;
            sg_reconstruction sgr = new sg_reconstruction();
            GameObject newSculpture = sgr.Reconstruct(path, out resTree, out currentLeft);

            currentHashTree.IngestTreeDontSetRoot(resTree);
            

            if(!gameObjects.Contains(leftGrabbedObject )) {
                gameObjects.Add(leftGrabbedObject);
            }
            gameObjects.Add(newSculpture);

            for(int i = sgr.toFree.Count - 1; i >= 0 ; i--) {
               intPtrs.Add(sgr.toFree[i]);
            }

            HideGameObject(leftGrabbedObject);

            newSculpture.transform.position = leftGrabbedObject.transform.position;
            newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;

            leftGrabbedObject = newSculpture;

    }

    void LoadBrush(string path) {
        sg_reconstruction sgr = new sg_reconstruction();
        HashTree resTree;
        GameObject newTool = sgr.Reconstruct(path, out resTree, out currentRight);
        currentHashTree.IngestTreeDontSetRoot(resTree);
        if(!gameObjects.Contains(rightGrabbedObject )) {
            gameObjects.Add(rightGrabbedObject);
        }
        gameObjects.Add(newTool);

        for(int i = sgr.toFree.Count - 1; i >= 0 ; i--) {
            intPtrs.Add(sgr.toFree[i]);
        }

        HideGameObject(rightGrabbedObject);

        newTool.transform.position = rightGrabbedObject.transform.position;
        newTool.transform.rotation = rightGrabbedObject.transform.rotation;

        rightGrabbedObject = newTool;
    }
}
