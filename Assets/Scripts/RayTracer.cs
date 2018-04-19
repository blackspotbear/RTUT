using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class RayTracer : MonoBehaviour {

    public RawImage m_image;

    // Use this for initialization
    void Start() {
        var texture = rayTrace(new Texture2D(640, 480));
        texture.Apply();
        m_image.texture = texture;
        Debug.Log("Hello " + this.m_image.mainTexture.width + "," + this.m_image.mainTexture.height);
    }

    // Update is called once per frame
    void Update() {

    }

    static private Texture2D rayTrace(Texture2D texture) {
        for (var y = 4; y < texture.height - 4; y++) {
            for (var x = 4; x < texture.width - 4; x++) {
                var color = x < texture.width / 3 ? Color.red : x < texture.width / 3 * 2 ? Color.blue : Color.green;
                texture.SetPixel(x, y, color);
            }
        }
        return texture;
    }
}
