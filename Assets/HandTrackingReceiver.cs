using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Collections.Generic;

public class HandTrackingReceiver : MonoBehaviour
{
    private UdpClient udpClient;
    private int port = 5005; // Set your port number
    private IPEndPoint ipEndPoint;

    // Serialized fields for bone transforms (to assign in the Inspector)
    [SerializeField] private Transform wristBone;
    [SerializeField] private Transform palmBone;
    [SerializeField] private Transform indexBase;
    [SerializeField] private Transform indexMid;
    [SerializeField] private Transform indexTip;
    [SerializeField] private Transform thumbBase;
    [SerializeField] private Transform thumbMid;
    [SerializeField] private Transform thumbTip;
    [SerializeField] private Transform middleBase;
    [SerializeField] private Transform middleMid;
    [SerializeField] private Transform middleTip;
    [SerializeField] private Transform ringBase;
    [SerializeField] private Transform ringMid;
    [SerializeField] private Transform ringTip;
    [SerializeField] private Transform pinkyBase;
    [SerializeField] private Transform pinkyMid;
    [SerializeField] private Transform pinkyTip;

    void Start()
    {
        // Initialize UDP client and listener
        udpClient = new UdpClient(port);
        ipEndPoint = new IPEndPoint(IPAddress.Any, port);
        udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null); // Start receiving data
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
        byte[] data = udpClient.EndReceive(ar, ref endPoint);

        // Parse the received data (e.g., hand landmarks in JSON format)
        string receivedMessage = System.Text.Encoding.UTF8.GetString(data);
        Debug.Log("Received Data: " + receivedMessage);

        // Process the data (map it to hand movement)
        ProcessReceivedData(receivedMessage);

        // Continue listening for the next packet
        udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
    }

    private void ProcessReceivedData(string data)
    {
        // Deserialize the JSON data (ensure the structure matches what you are sending)
        Dictionary<int, Dictionary<string, float>> handData = JsonUtility.FromJson<HandData>(data).handData;

        // Map the hand landmarks to the corresponding bones
        if (handData.ContainsKey(0)) wristBone.localPosition = new Vector3(handData[0]["x"], handData[0]["y"], handData[0]["z"]);
        if (handData.ContainsKey(1)) indexBase.localPosition = new Vector3(handData[1]["x"], handData[1]["y"], handData[1]["z"]);
        if (handData.ContainsKey(2)) indexMid.localPosition = new Vector3(handData[2]["x"], handData[2]["y"], handData[2]["z"]);
        if (handData.ContainsKey(3)) indexTip.localPosition = new Vector3(handData[3]["x"], handData[3]["y"], handData[3]["z"]);
        if (handData.ContainsKey(4)) thumbBase.localPosition = new Vector3(handData[4]["x"], handData[4]["y"], handData[4]["z"]);
        if (handData.ContainsKey(5)) thumbMid.localPosition = new Vector3(handData[5]["x"], handData[5]["y"], handData[5]["z"]);
        if (handData.ContainsKey(6)) thumbTip.localPosition = new Vector3(handData[6]["x"], handData[6]["y"], handData[6]["z"]);
        if (handData.ContainsKey(7)) middleBase.localPosition = new Vector3(handData[7]["x"], handData[7]["y"], handData[7]["z"]);
        if (handData.ContainsKey(8)) middleMid.localPosition = new Vector3(handData[8]["x"], handData[8]["y"], handData[8]["z"]);
        if (handData.ContainsKey(9)) middleTip.localPosition = new Vector3(handData[9]["x"], handData[9]["y"], handData[9]["z"]);
        if (handData.ContainsKey(10)) ringBase.localPosition = new Vector3(handData[10]["x"], handData[10]["y"], handData[10]["z"]);
        if (handData.ContainsKey(11)) ringMid.localPosition = new Vector3(handData[11]["x"], handData[11]["y"], handData[11]["z"]);
        if (handData.ContainsKey(12)) ringTip.localPosition = new Vector3(handData[12]["x"], handData[12]["y"], handData[12]["z"]);
        if (handData.ContainsKey(13)) pinkyBase.localPosition = new Vector3(handData[13]["x"], handData[13]["y"], handData[13]["z"]);
        if (handData.ContainsKey(14)) pinkyMid.localPosition = new Vector3(handData[14]["x"], handData[14]["y"], handData[14]["z"]);
        if (handData.ContainsKey(15)) pinkyTip.localPosition = new Vector3(handData[15]["x"], handData[15]["y"], handData[15]["z"]);
    }

    // Class to deserialize the hand data from JSON
    [Serializable]
    public class HandData
    {
        public Dictionary<int, Dictionary<string, float>> handData;
    }

    void OnApplicationQuit()
    {
        // Close UDP connection on exit
        udpClient.Close();
    }
}
