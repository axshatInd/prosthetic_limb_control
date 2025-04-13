using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using System.Linq;

public class HandTrackingReceiver : MonoBehaviour
{
    public int port = 6000;
    private UdpClient udpClient;

    // Hand bone references
    public Transform wrist;
    public Transform[] thumbJoints;
    public Transform[] indexJoints;
    public Transform[] middleJoints;
    public Transform[] ringJoints;
    public Transform[] pinkyJoints;

    // Settings
    public float movementScale = 0.2f;
    public float rotationSmoothness = 8f;
    public float fingerCurlSensitivity = 45f;
    
    // Direction control
    public bool invertThumb = false;
    public bool invertFingers = true;
    
    // Debug options
    public bool debugMode = true;
    
    // Rest pose recovery
    private bool isHandVisible = false;
    private float handVisibilityTimeout = 0.5f;
    private float lastHandTime = 0f;

    [System.Serializable]
    private class HandData { public Landmark[] landmark; }

    [System.Serializable]
    private class Landmark { public float x; public float y; public float z; }

    private Landmark[] currentLandmarks;
    private Quaternion[] defaultRotations;

    void Start()
    {
        InitializeUDP();
        StoreDefaultRotations();
    }

    void StoreDefaultRotations()
    {
        var allJoints = new Transform[][] {
            thumbJoints, indexJoints, middleJoints, ringJoints, pinkyJoints
        };
        
        int totalJoints = allJoints.Sum(arr => arr.Length);
        defaultRotations = new Quaternion[totalJoints];
        
        int index = 0;
        foreach (var jointArray in allJoints)
        {
            foreach (var joint in jointArray)
            {
                defaultRotations[index++] = joint.localRotation;
            }
        }
    }

    void InitializeUDP()
    {
        try
        {
            udpClient = new UdpClient(port);
            udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP Init Error: {e.Message}");
        }
    }

    private void ReceiveCallback(IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);
            byte[] data = udpClient.EndReceive(result, ref remoteEP);
            ProcessData(data);
            udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }
        catch (Exception e)
        {
            Debug.LogError($"UDP Error: {e.Message}");
        }
    }

    void ProcessData(byte[] data)
    {
        try
        {
            string json = Encoding.UTF8.GetString(data);
            HandData handData = JsonConvert.DeserializeObject<HandData>(json);
            currentLandmarks = handData.landmark;
            lastHandTime = Time.time;
            isHandVisible = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Data Error: {e.Message}");
        }
    }

    void Update()
    {
        if (currentLandmarks != null && currentLandmarks.Length >= 21)
        {
            UpdateWrist();
            UpdateFingers(thumbJoints, 1, true);
            UpdateFingers(indexJoints, 5, false);
            UpdateFingers(middleJoints, 9, false);
            UpdateFingers(ringJoints, 13, false);
            UpdateFingers(pinkyJoints, 17, false);
        }
        else if (Time.time - lastHandTime > handVisibilityTimeout)
        {
            isHandVisible = false;
            ReturnToRestPose();
        }
    }

    void UpdateWrist()
    {
        Vector3 wristPos = ConvertToVector3(currentLandmarks[0]);
        wrist.position = wristPos * movementScale;

        Vector3 palmForward = (ConvertToVector3(currentLandmarks[9]) - wristPos).normalized;
        Vector3 palmUp = Vector3.Cross(
            ConvertToVector3(currentLandmarks[5]) - ConvertToVector3(currentLandmarks[17]),
            palmForward).normalized;

        Quaternion targetRotation = Quaternion.LookRotation(palmForward, palmUp);
        wrist.rotation = Quaternion.Slerp(wrist.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
    }

    void UpdateFingers(Transform[] joints, int startIdx, bool isThumb)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            Vector3 startPos = ConvertToVector3(currentLandmarks[startIdx + i]);
            Vector3 endPos = ConvertToVector3(currentLandmarks[startIdx + i + 1]);
            
            // Calculate curl based on z-distance (depth)
            float zDistance = endPos.z - startPos.z;
            
            // Invert bending direction based on settings
            if ((isThumb && invertThumb) || (!isThumb && invertFingers)) {
                zDistance = -zDistance;
            }
            
            float bendFactor = Mathf.Clamp01(Mathf.Abs(zDistance) * fingerCurlSensitivity);
            
            // Joint-specific scaling with reduced values
            float jointScale = 1.3f;
            if (i == 0) jointScale = 1.2f;
            if (i == 2) jointScale = 1.4f;
            
            bendFactor *= jointScale;
            
            Quaternion targetRotation;
            
            if (isThumb)
            {
                // Thumb rotation logic remains unchanged
                if (i == 0) // Base joint (CMC)
                {
                    targetRotation = Quaternion.Euler(
                        bendFactor * 30f,
                        bendFactor * 50f,
                        bendFactor * 15f
                    );
                }
                else if (i == 1) // Middle joint (MCP)
                {
                    targetRotation = Quaternion.Euler(
                        bendFactor * 70f,
                        bendFactor * 10f,
                        bendFactor * 5f
                    );
                }
                else // Tip joint (IP)
                {
                    targetRotation = Quaternion.Euler(
                        bendFactor * 80f,
                        0f,
                        0f
                    );
                }
            }
            else
            {
                // Regular fingers - invert tip joint specifically
                float maxAngle = 100f;
                if (i == 1) maxAngle = 120f;
                if (i == 2) maxAngle = 110f; // Negative value for tip joint inversion
                
                targetRotation = Quaternion.Euler(bendFactor * maxAngle, 0f, 0f);
            }
            
            // Apply rotation with original rotation as basis
            int arrayIndex = GetJointArrayIndex(joints) + i;
            if (arrayIndex < defaultRotations.Length)
            {
                targetRotation = defaultRotations[arrayIndex] * targetRotation;
            }
            
            joints[i].localRotation = Quaternion.Slerp(
                joints[i].localRotation,
                targetRotation,
                Time.deltaTime * rotationSmoothness * 2.0f
            );
            
            if (debugMode && bendFactor > 0.1f)
            {
                Debug.Log($"{joints[i].name}: Bend {bendFactor * 100}%");
            }
        }
    }

    private int GetJointArrayIndex(Transform[] jointArray)
    {
        if (jointArray == thumbJoints) return 0;
        if (jointArray == indexJoints) return thumbJoints.Length;
        if (jointArray == middleJoints) return thumbJoints.Length + indexJoints.Length;
        if (jointArray == ringJoints) return thumbJoints.Length + indexJoints.Length + middleJoints.Length;
        return thumbJoints.Length + indexJoints.Length + middleJoints.Length + ringJoints.Length;
    }

    Vector3 ConvertToVector3(Landmark landmark)
    {
        return new Vector3(
            landmark.x * movementScale,
            landmark.y * movementScale,
            landmark.z * movementScale
        );
    }

    void ReturnToRestPose()
    {
        int index = 0;
        foreach (var jointArray in new Transform[][] { thumbJoints, indexJoints, middleJoints, ringJoints, pinkyJoints })
        {
            foreach (var joint in jointArray)
            {
                if (index < defaultRotations.Length)
                {
                    joint.localRotation = Quaternion.Slerp(
                        joint.localRotation,
                        defaultRotations[index],
                        Time.deltaTime * (rotationSmoothness * 0.5f)
                    );
                }
                index++;
            }
        }
    }

    void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient.Dispose();
        }
    }
}
