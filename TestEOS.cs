using Riptide;
using Riptide.Transports.Eos;
using TMPro;
using UnityEngine;

public class TestEOS : MonoBehaviour
{
    EosServer server;
    EosClient client;

    public TMP_Text currentaddress;

    private void Start() {
        EOSSDK.Initialize();
        server = new();
        client = new(server);
        client.Connected += Client_Connected;
    }

    public void CreateServer() {
        server.Start(7777);
        currentaddress.text = this.server.listenAddress;

        print($"server{server}, address:{this.server.listenAddress}");
        StartClient(server.listenAddress);
    }

    public void StartClient(string address) {
        client.Connect(address, out _, out _);
        print($"EOS SERVER: {this.server}");
        print($"client{client}, address:{address}");
    }

    private void Client_Connected(object sender, System.EventArgs e) {
        print($"Client: Is Connected: {e}");
    }

    public void JoinServer(string address) {
        StartClient(address);
    }

}
