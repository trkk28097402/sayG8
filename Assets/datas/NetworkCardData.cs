using Fusion;
using UnityEngine;

public struct NetworkedCardData : INetworkStruct
{
    public int cardId;  
    public NetworkString<_32> cardName;
    public NetworkString<_128> imagePath;
}