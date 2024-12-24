using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManagerClassrroom : MonoBehaviour
{
    [Header("----------- Audio Source -----------")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;

    [Header("----------- Audio Clip -----------")]
    public AudioClip Nyan_BGM;
    public AudioClip CardTouchSound;
    public AudioClip CardUseSound;

    private void Start()
    {
        musicSource.clip = Nyan_BGM;
        musicSource.Play();
    }
    public void PlaySoundEffectClassroom(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }

}