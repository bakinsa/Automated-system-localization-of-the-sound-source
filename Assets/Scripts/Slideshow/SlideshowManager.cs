using System.Collections;
using UnityEngine;

public class SlideshowManager : MonoBehaviour
{
  public const int FPS = 6;
  public const float DELTA_T = 1f / FPS;
  const int RING_DURATION = 2;
  const int RING_SIZE = FPS * RING_DURATION + 2;

  [SerializeField]
  private ViewportManager viewport;
  private WebCamTexture cam;
  public bool available
  {
    get;
    private set;
  }

  private Texture2D[] ring;
  private int ringPos;
  private SlideshowWriter writer;

  public bool IsRecording
  {
    get
    {
      return writer != null;
    }
  }

  void SetCamera(WebCamDevice device)
  {
    var rect = viewport.rect;
    if (device.isFrontFacing)
      cam = new WebCamTexture(device.name, 600, 300);
    else
      cam = new WebCamTexture(device.name, (int)rect.width / 2, (int)rect.height / 2);
    available = true;
    cam.Play();
  }

  void Start()
  {
    foreach (var device in WebCamTexture.devices)
    {
      if (!device.isFrontFacing)
      {
        SetCamera(device);
        break;
      }
    }
    if (!available)
    {
      if (WebCamTexture.devices.Length == 0)
      {
        Debug.LogError("No back camera found");
        Application.Quit();
        return;
      }
      else
      {
        SetCamera(WebCamTexture.devices[0]);
      }
    }
    ring = new Texture2D[RING_SIZE];
    for (int i = 0; i < RING_SIZE; ++i)
      ring[i] = new Texture2D(cam.width, cam.height);
    ringPos = 0;
    PlayerPrefs.SetInt("mirrored", cam.videoVerticallyMirrored ? 1 : 0);
    PlayerPrefs.SetInt("orient", -cam.videoRotationAngle);
    PlayerPrefs.Save();
    StartCoroutine(CaptureCycle());
  }

  public IEnumerator CaptureCycle()
  {
    while (available)
    {
      if (!cam.isPlaying)
      {
        yield return new WaitForSecondsRealtime(DELTA_T);
        continue;
      }
      var currTime = Time.time;
      yield return new WaitForEndOfFrame();
      ring[ringPos].SetPixels(cam.GetPixels());
      ring[ringPos].Apply();
      viewport.mirrored = cam.videoVerticallyMirrored;
      viewport.orient = -cam.videoRotationAngle;
      viewport.SetTexture(ring[ringPos]);
      if (writer != null)
      {
        var tex = new Texture2D(cam.width, cam.height);
        Graphics.CopyTexture(ring[ringPos], tex);
        tex.Apply();
        writer.Add(tex);
      }
      ringPos++;
      if (ringPos == RING_SIZE) ringPos = 0;
      var sleepTime = Time.time - currTime + DELTA_T;
      if (sleepTime > 0)
        yield return new WaitForSecondsRealtime(sleepTime);
    }
  }

  public void StartRecording()
  {
    var pos = ringPos;
    var ss = new SlideshowWriter(Application.persistentDataPath);
    var tex = new Texture2D(cam.width, cam.height);
    Graphics.CopyTexture(ring[pos == 0 ? RING_SIZE - 1 : pos - 1], tex);
    tex.Apply();
    ss.Add(tex);
    tex = new Texture2D(cam.width, cam.height);
    Graphics.CopyTexture(ring[pos], tex);
    tex.Apply();
    ss.Add(tex);
    writer = ss;
  }

  public string StopRecording()
  {
    var res = writer.Dispose();
    writer = null;
    return res;
  }

  public void Dispose()
  {
    cam.Stop();
  }
}
