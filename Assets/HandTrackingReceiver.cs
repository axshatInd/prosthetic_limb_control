using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

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
    public float movementScale = 0.15f;
    public float rotationSmoothness = 15f;

    [System.Serializable]
    private class HandData { public Landmark[] landmark; }

    [System.Serializable]
    private class Landmark { public float x; public float y; public float z; }

    private Landmark[] currentLandmarks;

    void Start()
    {
        InitializeUDP();
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
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Data Error: {e.Message}");
        }
    }

    void Update()
    {
        if (currentLandmarks == null || currentLandmarks.Length < 21) return;

        UpdateWrist();
        UpdateFingers(thumbJoints, 1);
        UpdateFingers(indexJoints, 5);
        UpdateFingers(middleJoints, 9);
        UpdateFingers(ringJoints, 13);
        UpdateFingers(pinkyJoints, 17);
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

    void UpdateFingers(Transform[] joints, int startIdx)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            Vector3 startPos = ConvertToVector3(currentLandmarks[startIdx + i]);
            Vector3 endPos = ConvertToVector3(currentLandmarks[startIdx + i + 1]);
            Vector3 direction = (endPos - startPos).normalized;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            joints[i].rotation = Quaternion.Slerp(
                joints[i].rotation, 
                targetRotation, 
                Time.deltaTime * rotationSmoothness
            );
        }
    }

    Vector3 ConvertToVector3(Landmark landmark)
    {
        return new Vector3(landmark.x, landmark.y, landmark.z);
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
