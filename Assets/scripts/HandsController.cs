using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandsController : MonoBehaviour
{
 // Start is called before the first frame update
    public GameObject leftHandCube;
    public GameObject rightHandCube;

    public GameObject sculpture;

    public OVRInput.Controller leftController;
    public OVRInput.Controller rightController;

    

    public void Start()
    {
        

    }

    public void setHandPos(GameObject x, OVRInput.Controller controller)
    {

        Vector3 pos = OVRInput.GetLocalControllerPosition(controller);
        Quaternion rot = OVRInput.GetLocalControllerRotation(controller);
        x.transform.localPosition = pos;
        x.transform.localRotation = rot;
    }

    public void setSculpturePose(OVRInput.Controller controller)
    {

        Debug.Log("Sculpture Pose");
        Vector3 pos = OVRInput.GetLocalControllerPosition(controller) + new Vector3(0, 0, 2.0f);
        Quaternion rot = OVRInput.GetLocalControllerRotation(controller);
        sculpture.transform.localPosition = pos;
        sculpture.transform.localRotation = rot;
    }

    // Update is called once per frame
    public void Update()
    {
       setHandPos(leftHandCube, leftController);
       setHandPos(rightHandCube, rightController);

       // if the left secondary trigger is pushed, rotate the sculpture
         if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.0f) 
         {
              setSculpturePose(leftController);
         }

    }
}