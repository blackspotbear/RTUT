using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

class Ray {
    public Ray() { }
    public Ray(Vector3 a, Vector3 b) { A = a; B = b; }

    public Vector3 origin() { return A; }
    public Vector3 direction() { return B; }
    public Vector3 point_at_parameter(float t) { return A + B * t; }

    public Vector3 A;
    public Vector3 B;
}

public class RayTracer : MonoBehaviour {

    public RawImage m_image;

    void Start() {
        var texture = rayTrace(new Texture2D(640, 320));
        texture.Apply();
        m_image.texture = texture;
        Debug.Log("Hello " + this.m_image.mainTexture.width + "," + this.m_image.mainTexture.height);
    }

    void Update() {
        
    }

    static private Color color(Ray r) {
        if (hit_sphere(new Vector3(0, 0, -1), 0.5f, r))
            return Color.red;
        var unit_direction = r.direction().normalized;
        var t = 0.5f * (unit_direction.y + 1.0f);
        return (1.0f - t) * (new Color(1, 1, 1)) + t * (new Color(0.5f, 0.7f, 1.0f));
    }

    static private bool hit_sphere(Vector3 center, float radius, Ray r) {
        var oc = r.origin() - center;
        var a = Vector3.Dot(r.direction(), r.direction());
        var b = 2 * Vector3.Dot(oc, r.direction());
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var discrement = b * b - 4 * a * c;
        return discrement > 0;
    }

    static private Texture2D rayTrace(Texture2D texture) {
        var lower_left_corner = new Vector3(-2, -1, -1);
        var horizontal = new Vector3(4, 0, 0);
        var vertical = new Vector3(0, 2, 0);
        var origin = new Vector3(0, 0, 0);

        for (var j = texture.height - 1; j >= 0; j--) {
            for (var i = 0; i < texture.width; i++) {
                var u = (float)i / texture.width;
                var v = (float)j / texture.height;
                var ray = new Ray(origin, lower_left_corner + horizontal * u + vertical * v);
                var col = color(ray);
                texture.SetPixel(i, j, col );
            }
        }

        return texture;
    }
}
