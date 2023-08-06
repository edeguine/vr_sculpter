using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SolidGeometryLib;
using System.Runtime.InteropServices;
using System;
using System.IO;
using UnityEditor;


public class Sculpture : ScriptableObject {
    
    public GameObject tgameObject;

    public List<IntPtr> previousVersion;
    public List<HashTreeNode> previousVersionTree;

    public Stack<IntPtr> nextVersion;
    public Stack<HashTreeNode> nextVersionTree;

    public IntPtr currentVersion;
    public HashTreeNode currentVersionTree;

    public List<IntPtr> intPtrsToFree;
    public List<GameObject> gameObjectstoFree;

    public HashTree currentHashTree;

    public void init() {
        // memory
        intPtrsToFree = new List<IntPtr>();
        gameObjectstoFree = new List<GameObject>();

        // creation
        IntPtr initPtr = SolidGeometryLibIntegration.sg_object_box(1.0f, 1.0f, 1.0f);
        // removeOffset
        SolidGeometryLibIntegration.sg_object_move(initPtr, -0.5f, -0.5f, -0.5f);

        intPtrsToFree.Add(initPtr);
        tgameObject = new GameObject();
        gameObjectstoFree.Add(tgameObject);

        sgObject sgObj = tgameObject.AddComponent<sgObject>();
        sgObj.InitObject(initPtr);

        previousVersion = new List<IntPtr>();
        previousVersionTree = new List<HashTreeNode>();

        nextVersion = new Stack<IntPtr>();
        nextVersionTree = new Stack<HashTreeNode>();
        

        // history
        currentHashTree = new HashTree();
        currentVersion = sgObj.GetHandle();
        currentVersionTree = new HashTreeNode("sg_object_box", new List<float>{1.0f, 1.0f, 1.0f});
        currentVersionTree = sg_export.move(currentVersionTree, currentHashTree, -0.5f, -0.5f, -0.5f);

        currentHashTree.addNodeNoRelatives(currentVersionTree);
        currentHashTree.setRoot(currentVersionTree);

        previousVersion.Add(currentVersion);
        previousVersionTree.Add(currentVersionTree);
    }

    public void setTransforms(Vector3 position, Quaternion rotation) {
        if(position != null) {
            tgameObject.transform.position = position;
        }
        if(rotation != null) {
            tgameObject.transform.rotation = rotation;
        }
    }

    public void Undo() {
        if(previousVersion.Count > 0 && previousVersionTree.Count > 0) {
            int index = previousVersion.Count - 1;
            IntPtr ptr = previousVersion[index];
            HashTreeNode tree = previousVersionTree[index];

            intPtrsToFree.Add(ptr);
            intPtrsToFree.Add(currentVersion);


            nextVersion.Push(currentVersion);
            nextVersionTree.Push(currentVersionTree);

            currentVersion = ptr;
            currentVersionTree = tree;

            previousVersion.RemoveAt(index);
            previousVersionTree.RemoveAt(index);

            updateGameObject(ptr, tree);
            Debug.Log("Undid");
        } else {
            Debug.Log("Nothing to undo");
        }
    }

    public void Redo() {
        if(nextVersion.Count > 0 && nextVersionTree.Count > 0) {
            IntPtr ptr = nextVersion.Pop();
            HashTreeNode tree = nextVersionTree.Pop();

            intPtrsToFree.Add(ptr);
            intPtrsToFree.Add(currentVersion);

            previousVersion.Add(currentVersion);
            previousVersionTree.Add(currentVersionTree);

            currentVersion = ptr;
            currentVersionTree = tree;

            updateGameObject(ptr, tree);
            Debug.Log("Redid");
        } else {
            Debug.Log("Nothing to redo");
        }
    }

    void Update() {}
    void Start() {}

    public void Scale(float factor) {
        
    }

    public void CleanDestroy()
    {   
        Debug.Log("CleanDestroy");
        Debug.Log("gameObjects.Count: " + gameObjectstoFree.Count.ToString());
        Debug.Log("intPtrsToFree.Count: " + intPtrsToFree.Count.ToString());

        var alreadyFreedGameObjects = new HashSet<GameObject>();
        var alreadyFreed = new HashSet<IntPtr>();

        int destroyedGameObjectsCount = 0;
        
        for(int i = gameObjectstoFree.Count - 1; i >= 0; i--) {
            
            if(gameObjectstoFree[i] == null) {
                continue;
            }
            if(alreadyFreedGameObjects.Contains(gameObjectstoFree[i])) {
                continue;
            }

            if(gameObjectstoFree[i].GetComponent<sgObject>() != null) {
                IntPtr ptr = gameObjectstoFree[i].GetComponent<sgObject>().GetHandle();
                alreadyFreed.Add(ptr);
            }
            DestroyImmediate(gameObjectstoFree[i]);
            destroyedGameObjectsCount++;
            alreadyFreedGameObjects.Add(gameObjectstoFree[i]);
            
        }
        Debug.Log("destroyedGameObjectsCount: " + destroyedGameObjectsCount.ToString());
        // keep track of already freed pointers to avoid double free
        

        int destroyedIntPtrsCount = 0;
        for(int i = intPtrsToFree.Count - 1; i >= 0; i--) {
            if(intPtrsToFree[i] == IntPtr.Zero) {
                continue;
            }
            if(alreadyFreed.Contains(intPtrsToFree[i])) {
                continue;
            }
            SolidGeometryLibIntegration.sg_object_free(intPtrsToFree[i]);
            destroyedIntPtrsCount++;
            alreadyFreed.Add(intPtrsToFree[i]);
        }

        Debug.Log("destroyedIntPtrsCount: " + destroyedIntPtrsCount.ToString());

        intPtrsToFree.Clear();
        gameObjectstoFree.Clear();

    }

    Vector3 getNewPosition(GameObject brush) {
        return new Vector3(brush.transform.position.x,
            brush.transform.position.y,
            brush.transform.position.z);
    }

    Vector3 getRotationCenter(GameObject brush) {
        return new Vector3(0.0f, 0.0f, 0.0f);
    }

    Vector3 getNewPositionAccountOffset(GameObject brush) {
        return new Vector3(brush.transform.position.x + brush.transform.localScale.x / 2.0f,
            brush.transform.position.y + brush.transform.localScale.y / 2.0f,
            brush.transform.position.z + brush.transform.localScale.z / 2.0f);
    }

    Vector3 getRotationAxis(GameObject brush) {
        Quaternion rotationBrush = brush.transform.rotation;
        rotationBrush.ToAngleAxis(out float angleDegBrush, out Vector3 axisBrush);
        return axisBrush;
    }

    float getRotationAngleRad(GameObject brush) {
        Quaternion rotationBrush = brush.transform.rotation;
        rotationBrush.ToAngleAxis(out float angleDegBrush, out Vector3 axisBrush);
        float angleRadBrush = angleDegBrush * Mathf.Deg2Rad;
        return angleRadBrush;
    }

    
    IntPtr moveIntPtr(GameObject original, IntPtr ptr) {
        // warning this will move the stuff
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);
        Vector3 rotationCenter = getRotationCenter(original);

        SolidGeometryLibIntegration.sg_object_rotate(ptr,
            rotationCenter.x, rotationCenter.y, rotationCenter.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, rotationAngleRad);

        SolidGeometryLibIntegration.sg_object_move(ptr, 
            newPosition.x,
            newPosition.y,
            newPosition.z);
        return ptr;
    }

    
    private IntPtr moveIntPtrInverse(GameObject original, IntPtr ptr) {
        // warning this will move the stuff
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);
        Vector3 rotationCenter = getRotationCenter(original);

        SolidGeometryLibIntegration.sg_object_move(ptr, 
            -newPosition.x,
            -newPosition.y,
            -newPosition.z);

        SolidGeometryLibIntegration.sg_object_rotate(ptr,
            rotationCenter.x, rotationCenter.y, rotationCenter.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, -rotationAngleRad);
        
        return ptr;
    }

    private IntPtr moveIntPtrInverseNoOffset(GameObject original, IntPtr ptr) {
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

    HashTreeNode moveTree(GameObject original, HashTreeNode node) {
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);

        node = sg_export.rotate(node, currentHashTree, original.transform.position.x, original.transform.position.y, original.transform.position.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, rotationAngleRad);
        node = sg_export.move(node, currentHashTree, newPosition.x + original.transform.localScale.x / 2.0f, 
            newPosition.y + original.transform.localScale.y / 2.0f,
            newPosition.z + original.transform.localScale.z / 2.0f);
        return node;
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

    public void updateGameObject(IntPtr res, HashTreeNode tree_res) {
        previousVersion.Add(currentVersion);
        intPtrsToFree.Add(currentVersion);
        currentVersion = res;

        previousVersionTree.Add(currentVersionTree);
        currentVersionTree = tree_res;
        
        if(res == IntPtr.Zero) {
            Debug.Log("IntPtr is null");
            return;
        }

        intPtrsToFree.Add(res);

        GameObject resGO = new GameObject();
        resGO.transform.position = tgameObject.transform.position;
        resGO.transform.rotation = tgameObject.transform.rotation;
        sgObject resSgObj = resGO.AddComponent<sgObject>();
        resSgObj.InitObject(res);


        var original_name = tgameObject.name;
        tgameObject.name = original_name + "_old";

        Debug.Log("Old coordinates: " + tgameObject.transform.position.ToString() + " " + tgameObject.transform.rotation.ToString());
        HideGameObject(tgameObject);
        gameObjectstoFree.Add(tgameObject);

        tgameObject = resGO;
        tgameObject.name = original_name + "_new";
        Debug.Log("New coordinates: " + tgameObject.transform.position.ToString() + " " + tgameObject.transform.rotation.ToString());
        gameObjectstoFree.Add(tgameObject);
    }

    public void load(string path) {

    }

    public void Add(Sculpture brush) {
        
        moveIntPtr(tgameObject, currentVersion);
        moveTree(tgameObject, currentVersionTree);

        moveIntPtr(brush.tgameObject, brush.currentVersion);
        moveTree(brush.tgameObject, brush.currentVersionTree);

        Debug.Log("Adding ");
        IntPtr res = boolean_add(currentVersion, brush.currentVersion);
        HashTreeNode tree_res = sg_export.add(currentVersionTree, brush.currentVersionTree, currentHashTree);

        intPtrsToFree.Add(res);

        moveIntPtrInverse(tgameObject, currentVersion);
        moveTreeInverse(tgameObject, currentVersionTree); // might be optional

        moveIntPtrInverse(brush.tgameObject, brush.currentVersion);
        moveTreeInverse(brush.tgameObject, brush.currentVersionTree);

        if(res != IntPtr.Zero) {
            moveIntPtrInverse(tgameObject, res);
            moveTreeInverse(tgameObject, tree_res);

            updateGameObject(res, tree_res);
            Debug.Log("Added ");
        } else {
            Debug.Log("Addition empty");
        }
    }

    public void Sub(Sculpture brush) {
        
        moveIntPtr(tgameObject, currentVersion);
        moveTree(tgameObject, currentVersionTree);

        moveIntPtr(brush.tgameObject, brush.currentVersion);
        moveTree(brush.tgameObject, brush.currentVersionTree); 

        Debug.Log("Subtracting ");
        IntPtr res = boolean_sub(currentVersion, brush.currentVersion);
        HashTreeNode tree_res = sg_export.sub(currentVersionTree, brush.currentVersionTree, currentHashTree);

        intPtrsToFree.Add(res);

        moveIntPtrInverse(tgameObject, currentVersion);
        moveTreeInverse(tgameObject, currentVersionTree); // might be optional

        moveIntPtrInverse(brush.tgameObject, brush.currentVersion);
        moveTreeInverse(brush.tgameObject, brush.currentVersionTree); 

        if(res != IntPtr.Zero) {
            moveIntPtrInverse(tgameObject, res);
            moveTreeInverse(tgameObject, tree_res);

            updateGameObject(res, tree_res);
            Debug.Log("Subtracted ");
        } else {
            Debug.Log("Subtraction empty");
        }
        
    }

    public void test(Sculpture brush) {

        moveIntPtr(tgameObject, currentVersion);
        moveIntPtr(brush.tgameObject, brush.currentVersion);

        updateGameObject(currentVersion, currentVersionTree);
        brush.updateGameObject(brush.currentVersion, brush.currentVersionTree);

    }

    public void testRotation() {
        moveIntPtr(tgameObject, currentVersion);
        moveTree(tgameObject, currentVersionTree);

        moveIntPtrInverse(tgameObject, currentVersion);
        moveTreeInverse(tgameObject, currentVersionTree);

        updateGameObject(currentVersion, currentVersionTree);
    }

    void HideGameObject(GameObject obj) {
        gameObjectstoFree.Add(obj);
        if (obj != null ) {
            Renderer rend = obj.GetComponent<Renderer>();
            rend.enabled = false;

            if(obj.GetComponent<sgObject>() != null) {
                intPtrsToFree.Add(obj.GetComponent<sgObject>().GetHandle());
            }
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
                        intPtrsToFree.Add(curCh);
                        res = curCh;
                    }
                    SolidGeometryLibIntegration.sg_group_break(subResGroup);
                    intPtrsToFree.Add(subResGroup);
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
                        intPtrsToFree.Add(curCh);
                        res = curCh;
                    }
                    SolidGeometryLibIntegration.sg_group_break(unionResGroup);
                    intPtrsToFree.Add(unionResGroup);
                }
            }
            return res;
    }
}





public class Sculpt : MonoBehaviour
{

    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;

    public float leftRotationAmplification;
    public float rightRotationAmplification;
    public float distMultiplier = 2.0f;

    private Sculpture sculpture;
    private Sculpture brush;

    // list of all game objects created
    private List<GameObject> gameObjects = new List<GameObject>();

    private sg_reconstruction reconstruction = new sg_reconstruction();

    private bool isGrabbing = false;

    private float timeSinceLastSculpt = 0.0f;
    private float timeSinceLastScale = 0.0f;

    private Vector3 lastLeftHandPosition = new Vector3(0.0f, 0.0f, 2.0f);
    private Vector3 lastLeftPalmPosition = Vector3.zero;
    private Quaternion lastLeftHandRotation = Quaternion.identity;

    private Vector3 lastRightHandPosition = new Vector3(0.0f, 0.0f, 2.0f);
    private Vector3 lastRightPalmPosition = Vector3.zero;
    private Quaternion lastRightHandRotation = Quaternion.identity;

    private bool sgStarted = false;
    private bool sgStopped = false;

    private bool isAddMode = false;

    private int toolIndex = 0;
    private string pointePath = @"C:\Users\edegu\vr_sculpter\Assets\tools\pointe.txt";
    private string testPath = @"C:\Users\edegu\vr_sculpter\Assets\tools\simple_two_cubes.txt";
    private string smallCubePath = @"C:\Users\edegu\sculptures\small_cube_2023_07_22_12_22_17.txt";

    private List<String> defaultToolsPaths = new List<String>();

    void rotationTest() {

        IntPtr box = SolidGeometryLibIntegration.sg_object_box(1.0f, 1.0f, 1.0f);
        SolidGeometryLibIntegration.sg_object_move(box, -0.5f, -0.5f, -0.5f);

        SolidGeometryLibIntegration.sg_object_rotate(box, 0, 0, 0, 0f, 0.0f, 1.0f, Mathf.PI / 4.0f);

        GameObject display = new GameObject("display");
        sgObject sgObj = display.AddComponent<sgObject>();
        sgObj.InitObject(box);

    }


    void Start()
    {
        defaultToolsPaths.Add(pointePath);
        defaultToolsPaths.Add(testPath);
        defaultToolsPaths.Add(smallCubePath);
        toolIndex = 0;

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

        //LoadBrush(defaultToolsPaths[toolIndex]);
        //debugPositionWithLeft(rightGrabbedObject);
        

        sculpture = new Sculpture();
        sculpture.init();
        sculpture.tgameObject.name = "sculpture";

        Quaternion trotation = Quaternion.Euler(55, 12, 45);

        

        sculpture.setTransforms(new Vector3(0, 2, 3), trotation);
        sculpture.testRotation();

        brush = new Sculpture();
        brush.init();
        brush.tgameObject.name = "brush";

        // get a 25 degrees rotation along z as a quaternion using euler angles
        Quaternion rotation = Quaternion.Euler(0, 0, 10);

        brush.setTransforms(new Vector3(0, 2, 3), rotation);

        //sculpture.test(brush);
    }

    void CleanDestroy(List<GameObject> gameObjectstoFree, List<IntPtr> intPtrsToFree) {
        Debug.Log("CleanDestroy");
        Debug.Log("gameObjectstoFree.Count: " + gameObjectstoFree.Count.ToString());
        Debug.Log("intPtrsToFree.Count: " + intPtrsToFree.Count.ToString());

        var alreadyFreedGameObjects = new HashSet<GameObject>();
        var alreadyFreed = new HashSet<IntPtr>();

        int destroyedGameObjectsCount = 0;
        
        for(int i = gameObjectstoFree.Count - 1; i >= 0; i--) {
            
            if(gameObjectstoFree[i] == null) {
                continue;
            }
            if(alreadyFreedGameObjects.Contains(gameObjectstoFree[i])) {
                continue;
            }

            if(gameObjectstoFree[i].GetComponent<sgObject>() != null) {
                IntPtr ptr = gameObjectstoFree[i].GetComponent<sgObject>().GetHandle();
                alreadyFreed.Add(ptr);
            }
            DestroyImmediate(gameObjectstoFree[i]);
            destroyedGameObjectsCount++;
            alreadyFreedGameObjects.Add(gameObjectstoFree[i]);
            
        }
        Debug.Log("destroyedGameObjectsCount: " + destroyedGameObjectsCount.ToString());
        // keep track of already freed pointers to avoid double free
        

        int destroyedIntPtrsCount = 0;
        for(int i = intPtrsToFree.Count - 1; i >= 0; i--) {
            if(intPtrsToFree[i] == IntPtr.Zero) {
                continue;
            }
            if(alreadyFreed.Contains(intPtrsToFree[i])) {
                continue;
            }
            SolidGeometryLibIntegration.sg_object_free(intPtrsToFree[i]);
            destroyedIntPtrsCount++;
            alreadyFreed.Add(intPtrsToFree[i]);
        }

        Debug.Log("destroyedIntPtrsCount: " + destroyedIntPtrsCount.ToString());

        intPtrsToFree.Clear();
        gameObjectstoFree.Clear();
    }

    void OnDestroy() {
        Debug.Log("OnDestroy");
        

        var sculptureGameObjectsToFree = sculpture.gameObjectstoFree;
        var sculptureIntPtrsToFree = sculpture.intPtrsToFree;

        var brushGameObjectsToFree = brush.gameObjectstoFree;
        var brushIntPtrsToFree = brush.intPtrsToFree;


        // concatenate the lists
        // we need to destroy the sculpture first so we put them at the end, to avoid memory issues (a and b needs to be destroyed before a or b)
        var allGameObjectsToFree = brushGameObjectsToFree;
        allGameObjectsToFree.AddRange(sculptureGameObjectsToFree);

        var allIntPtrsToFree = brushIntPtrsToFree;
        allIntPtrsToFree.AddRange(sculptureIntPtrsToFree);

        CleanDestroy(allGameObjectsToFree, allIntPtrsToFree);


        if(sgStarted && !sgStopped) {
            Debug.Log("sgstop OnDestroy");
            SolidGeometryLibIntegration.sg_core_stop();
            sgStopped = true;
        }
    }

    void handleHand(OVRInput.Controller controller, Sculpture grabbedObject, bool isLeft)
    {
        if(isLeft && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) < 0.1f) {
            lastLeftPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastLeftHandRotation = OVRInput.GetLocalControllerRotation(controller);
            return;
        }
        if(isLeft && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) >= 0.1f) {
            var position = lastLeftHandPosition + distMultiplier * ( OVRInput.GetLocalControllerPosition(controller) - lastLeftPalmPosition);

            Quaternion changeInRotation = OVRInput.GetLocalControllerRotation(controller) * Quaternion.Inverse(lastLeftHandRotation);
            var rotation = changeInRotation * grabbedObject.tgameObject.transform.rotation;

            grabbedObject.setTransforms(position, rotation);

            lastLeftPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastLeftHandPosition = position;
            lastLeftHandRotation = OVRInput.GetLocalControllerRotation(controller);
        }

        if(!isLeft && OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) < 0.1f) {
            lastRightPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastRightHandRotation = OVRInput.GetLocalControllerRotation(controller);
            return;
        }
        if(!isLeft &&  OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) >= 0.1f) {
            Debug.Log("Grabbing with right hand");
            var position  = lastRightHandPosition + distMultiplier * ( OVRInput.GetLocalControllerPosition(controller) - lastRightPalmPosition);

            Quaternion changeInRotation = OVRInput.GetLocalControllerRotation(controller) * Quaternion.Inverse(lastRightHandRotation);
            var rotation = changeInRotation * grabbedObject.tgameObject.transform.rotation;

            grabbedObject.setTransforms(position, rotation);

            lastRightPalmPosition = OVRInput.GetLocalControllerPosition(controller);
            lastRightHandPosition = position;
            lastRightHandRotation = OVRInput.GetLocalControllerRotation(controller);
        }
    }


    void handleSculpt()
    {
        timeSinceLastSculpt += Time.deltaTime;

        if  (OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) >= 0.1f && !isGrabbing && timeSinceLastSculpt > 0.5f)
        {
            isGrabbing = true;
            Debug.Log("Perform sculpting " + UnityEngine.Random.Range(0, 1000).ToString());
            performSculpt();
            timeSinceLastSculpt = 0.0f;
        }
        else if (OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) < 0.1f && isGrabbing)
        {
            isGrabbing = false;
        }
    }

    void performSculpt() {
        if(isAddMode) {
            sculpture.Add(brush);
        } else {
            sculpture.Sub(brush);
        }
    }

    /*
    void moveGrabbedObjects() {
        leftGrabbedObject.transform.position = sculpture.tgameObject.transform.position;
        leftGrabbedObject.transform.rotation = sculpture.tgameObject.transform.rotation;

        rightGrabbedObject.transform.position = brush.tgameObject.transform.position;
        rightGrabbedObject.transform.rotation = brush.tgameObject.transform.rotation;
    }*/

    
    void Update() {
        
        handleHand(leftController, sculpture, true);
        handleHand(rightController, brush, false);
        //moveGrabbedObjects();

        handleSculpt();
        


        if(Input.GetKeyDown(KeyCode.S)) {
            string path = EditorUtility.SaveFilePanel("Save file as", "Assets/tools", "MyCoolSculptingTool", "txt");
            Debug.Log("Selected file: " + path);
            sg_export.writeToFile(sculpture.currentVersionTree, sculpture.currentHashTree, path);
        }

        if(OVRInput.GetDown(OVRInput.Button.One)) {
            sculpture.Undo();
        }

        if(OVRInput.GetDown(OVRInput.Button.Two)) {
            sculpture.Redo();
        }

        if(OVRInput.GetDown(OVRInput.Button.Three)) {
            // create a file called YYYY_MM_DD_HH_MM_SS.txt
            string path = @"C:\Users\edegu\sculptures\" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt";

            sg_export.writeToFile(sculpture.currentVersionTree, sculpture.currentHashTree, path);
            obj_exporter.MeshToFile(sculpture.tgameObject.GetComponent<MeshFilter>() , "Assets", "obj_export_" +  DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".obj");

        }

        if(OVRInput.GetDown(OVRInput.Button.Start)) {
            isAddMode = !isAddMode;
        }

        if(OVRInput.GetDown(OVRInput.Button.Four)) {
            toolIndex = (toolIndex + 1) % defaultToolsPaths.Count;
            Debug.Log("Tool index: " + toolIndex);
            //brush.load(defaultToolsPaths[toolIndex]);
        }

        if(Input.GetKeyDown(KeyCode.R)) {
            string path = EditorUtility.OpenFilePanel("Open file", "Assets/tools", "txt");
            Debug.Log("Selected file: " + path);
            brush.load(path);
        }

        if(Input.GetKeyDown(KeyCode.L)) {
            string path = EditorUtility.OpenFilePanel("Open file", "Assets/tools", "txt");
            Debug.Log("Selected file: " + path);
            sculpture.load(path);
        }


        // if right joystick up, scale up the brush
        timeSinceLastScale += Time.deltaTime;
        if(OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y > 0.5f && brush != null && timeSinceLastScale > 0.1f) {
            brush.Scale(1.05f);
            timeSinceLastScale = 0.0f;
        }

        if(OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y > 0.5f && brush != null && timeSinceLastScale > 0.1f) {
            brush.Scale(0.95f);
            timeSinceLastScale = 0.0f;
        }

        if(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y > 0.5f && sculpture != null && timeSinceLastScale > 0.1f) {
            sculpture.Scale(1.05f);
            timeSinceLastScale = 0.0f;
        }

        if(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y < -0.5f && sculpture != null && timeSinceLastScale > 0.1f) {
            sculpture.Scale(.95f);
            timeSinceLastScale = 0.0f;
        }

    }


    /*
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

    IntPtr moveIntPtr(GameObject original, IntPtr ptr) {
        // warning this will move the stuff
        Vector3 rotationAxis = getRotationAxis(original);
        float rotationAngleRad = getRotationAngleRad(original);
        Vector3 newPosition = getNewPosition(original);

        SolidGeometryLibIntegration.sg_object_rotate(ptr,
            original.transform.position.x, original.transform.position.y, original.transform.position.z,
            rotationAxis.x, rotationAxis.y, rotationAxis.z, rotationAngleRad);

        SolidGeometryLibIntegration.sg_object_move(ptr, 
            newPosition.x + original.transform.localScale.x / 2.0f, 
            newPosition.y + original.transform.localScale.y / 2.0f,
            newPosition.z + original.transform.localScale.z / 2.0f);
        return ptr;
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

            moveSgObjectInverse(leftGrabbedObject, currentLeftIntPtr);
            leftTrees.Add(currentLeft);
            leftIntPtrs.Add(currentLeftIntPtr);
        }

        intPtrs.Add(sculptureIntPtr);
        intPtrs.Add(brushIntPtr);
        intPtrs.Add(sculptedIntPtr);


        if(sculptedIntPtr == IntPtr.Zero) {
            Debug.Log("sculptedSgObject is null");
            currentLeft = moveTreeInverse(leftGrabbedObject, currentLeft);
            currentRight = moveTreeInverse(obrush, currentRight);
            if (sculptureIntPtr != IntPtr.Zero) {
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
            
            // clear up forward history
            leftTreeRightStack.Clear();
            leftIntPtrsRightStack.Clear();
            
            GameObject sculpture = new GameObject("Sculpture");
            sculpture.transform.position = new Vector3(0, 0, 0);
            sculpture.transform.rotation = new Quaternion(0, 0, 0, 0);

            moveSgObjectInverse(leftGrabbedObject, sculptedIntPtr);
            currentLeftIntPtr = sculptedIntPtr;
            tree = moveTreeInverse(leftGrabbedObject, tree);
            currentLeft = tree;


            currentRight = moveTreeInverse(obrush, currentRight);

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

    void UndoIntPtr() {
        if(leftTrees.Count == 0 || leftIntPtrs.Count == 0) {
            Debug.Log("No more undo");
            return;
        }
        Debug.Log("Undoing");
        leftTreeRightStack.Push(currentLeft);
        leftIntPtrsRightStack.Push(currentLeftIntPtr);

        HashTreeNode lastTree = leftTrees[leftTrees.Count - 1];
        leftTrees.RemoveAt(leftTrees.Count - 1);
        currentLeft = lastTree;
        IntPtr lastIntPtr = leftIntPtrs[leftIntPtrs.Count - 1];
        leftIntPtrs.RemoveAt(leftIntPtrs.Count - 1);
        currentLeftIntPtr = lastIntPtr;
        HideGameObject(leftGrabbedObject);
        
        
        GameObject newSculpture = new GameObject("Sculpture");
        //moveIntPtrInverse(leftGrabbedObject, lastIntPtr);
        newSculpture.AddComponent<sgObject>().InitObject(lastIntPtr);
        newSculpture.transform.position = leftGrabbedObject.transform.position;
        newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;
        
        leftGrabbedObject = newSculpture;

        gameObjects.Add(leftGrabbedObject);
    }

    void RedoIntPtr() {
        if(leftTreeRightStack.Count == 0 || leftIntPtrsRightStack.Count == 0) {
            Debug.Log("No more redo");
            return;
        }
        Debug.Log("Redoing");
        leftTrees.Add(currentLeft);
        leftIntPtrs.Add(currentLeftIntPtr);

        HashTreeNode lastTree = leftTreeRightStack.Pop();
        currentLeft = lastTree;
        IntPtr lastIntPtr = leftIntPtrsRightStack.Pop();
        currentLeftIntPtr = lastIntPtr;
        HideGameObject(leftGrabbedObject);

        GameObject newSculpture = new GameObject("Sculpture");
        
        newSculpture.AddComponent<sgObject>().InitObject(lastIntPtr);
        newSculpture.transform.position = leftGrabbedObject.transform.position;
        newSculpture.transform.rotation = leftGrabbedObject.transform.rotation;
        
        leftGrabbedObject = newSculpture;

        gameObjects.Add(leftGrabbedObject);
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

    
    */

    /*

    void FilledUpdate()
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
            toolIndex = (toolIndex + 1) % defaultToolsPaths.Count;
            Debug.Log("Tool index: " + toolIndex);
            LoadBrush(defaultToolsPaths[toolIndex]);
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


        // if right joystick up, scale up the brush
        timeSinceLastScale += Time.deltaTime;
        if(OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y > 0.5f && rightGrabbedObject != null && timeSinceLastScale > 0.1f) {
            ScaleObject(rightGrabbedObject, currentRight, 1.05f, false);
            timeSinceLastScale = 0.0f;
        }

        if(OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y < -0.5f && rightGrabbedObject != null && timeSinceLastScale > 0.1f) {
            ScaleObject(rightGrabbedObject, currentRight, 0.95f, false);
            timeSinceLastScale = 0.0f;
        }

        if(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y > 0.5f && leftGrabbedObject != null && timeSinceLastScale > 0.1f) {
            ScaleObject(leftGrabbedObject, currentLeft, 1.05f, true);
            timeSinceLastScale = 0.0f;
        }

        if(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y < -0.5f && leftGrabbedObject != null && timeSinceLastScale > 0.1f) {
            ScaleObject(leftGrabbedObject, currentLeft, 0.95f, true);
            timeSinceLastScale = 0.0f;
        }
    }


    void ScaleObject(GameObject obj, HashTreeNode node, float scale, bool isSculpture) {
        IntPtr objIntPtr = obj.GetComponent<sgObject>().GetHandle();
        SolidGeometryLibIntegration.sg_object_scale(objIntPtr, scale, scale, scale);
        HashTreeNode newNode = sg_export.scale(node, currentHashTree, scale, scale, scale);
        intPtrs.Add(objIntPtr);

        GameObject newObj = new GameObject("Sculpture");
        if(!isSculpture) {
            newObj.name = "Brush";
        }
        
        sgObject newSgObj = newObj.AddComponent<sgObject>();
        newSgObj.InitObject(objIntPtr);
        BoxCollider[] boxColliders = newObj.GetComponents<BoxCollider>();
        for(int i = 0; i < boxColliders.Length; i++) {
            DestroyImmediate(boxColliders[i]);
        }

        if(isSculpture) {
            newObj.transform.position = leftGrabbedObject.transform.position;
            newObj.transform.rotation = leftGrabbedObject.transform.rotation;
            RenameObject(leftGrabbedObject, "oldSculpture");
            gameObjects.Add(leftGrabbedObject);
            gameObjects.Add(newObj);
            HideGameObject(leftGrabbedObject);
            leftGrabbedObject = newObj;
            
        } else {
            newObj.transform.position = rightGrabbedObject.transform.position;
            newObj.transform.rotation = rightGrabbedObject.transform.rotation;
            RenameObject(rightGrabbedObject, "oldBrush");
            gameObjects.Add(rightGrabbedObject);
            gameObjects.Add(newObj);
            HideGameObject(rightGrabbedObject);
            rightGrabbedObject = newObj;
        }
        
        if(isSculpture) {
            // we cannot properly undo scaling without scaling the other way, so we just keep track
            // of current ptr and tree for saving purposes but not undoing purposes
            // TODO fix save / load after scaling, object seems to move far away
            if(objIntPtr != IntPtr.Zero) {
                currentLeftIntPtr = objIntPtr;
                //leftIntPtrs.Add(objIntPtr);
            }
            currentLeft = newNode;
            //leftTrees.Add(newNode);
        } else {
            if (objIntPtr != IntPtr.Zero) {
                currentRightIntPtr = objIntPtr;
            }
            currentRight = newNode;
        }
    }


    void LoadSculpture(string path) {
            HashTree resTree;
            sg_reconstruction sgr = new sg_reconstruction();
            Debug.Log("Reconstructing tree");
            GameObject newSculpture = sgr.Reconstruct(path, out resTree, out currentLeft);
            Debug.Log("Reingesting tree");

            currentHashTree.IngestTreeDontSetRoot(resTree);

            // clear leftTrees and rightTrees
            leftTrees.Clear();
            leftTreeRightStack.Clear(); 
            leftIntPtrs.Clear();
            leftIntPtrsRightStack.Clear();

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

            IntPtr newIntPtr = newSculpture.GetComponent<sgObject>().GetHandle();
            currentLeftIntPtr = newIntPtr;
            leftIntPtrs.Add(newIntPtr);
            leftTrees.Add(currentLeft);

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
    */
}
