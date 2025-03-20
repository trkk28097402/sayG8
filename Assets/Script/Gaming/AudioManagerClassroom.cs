using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManagerClassroom : MonoBehaviour
{
    [Header("----------- Audio Source -----------")]
    [SerializeField] public AudioSource musicSource;
    [SerializeField] public AudioSource SFXSource;

    [Header("----------- Audio Clip -----------")]
    public AudioClip Nyan_BGM;
    public AudioClip CardTouchSound;
    public AudioClip CardUseSound;

    public AudioClip victoryMusic;
    public AudioClip defeatMusic;


    private void Start()
    {
        musicSource.clip = Nyan_BGM;
        musicSource.Play();
    }
    public void PlaySoundEffectClassroom(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }

    public void PlayGameEndMusic(bool isWinner)
    {
        // Stop the current background music
        if (musicSource.isPlaying)
        {
            musicSource.Stop();
        }

        // Set the appropriate clip based on win/loss status
        musicSource.clip = isWinner ? victoryMusic : defeatMusic;

        // Play the music
        musicSource.Play();

        // Log which music is playing
        Debug.Log($"Playing {(isWinner ? "victory" : "defeat")} music");
    }
}