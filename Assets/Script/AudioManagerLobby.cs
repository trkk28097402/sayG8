using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManagerLobby : MonoBehaviour
{
    [Header("----------- Audio Source -----------")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;

    [Header("----------- Audio Clip -----------")]
    public AudioClip Lobby_BGM;
    public AudioClip ClickSound;

    private void Start()
    {
        musicSource.clip = Lobby_BGM;
        musicSource.Play();
    }
    public void PlaySoundEffectLobby(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }

}