using Fusion;
using UnityEngine;

public struct NetworkedCardData : INetworkStruct
{
    public NetworkString<_32> cardName;
    public NetworkString<_128> imagePath;
}