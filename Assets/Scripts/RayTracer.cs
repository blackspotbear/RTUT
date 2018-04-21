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

struct hit_record {
    public float t;
    public Vector3 p;
    public Vector3 normal;
}

abstract class hitable {
    public abstract bool hit(Ray r, float t_min, float t_max, out hit_record rec);
}

class hitable_list: List<hitable> {
    public bool hit(Ray r, float t_min, float t_max, out hit_record rec) {
        hit_record temp_rec;

        rec.normal = new Vector3();
        rec.p = new Vector3();
        rec.t = 0;

        var hit_anything = false;
        var closest_so_far = t_max;
        foreach (var h in this) {
            if (h.hit(r, t_min, closest_so_far, out temp_rec)) {
                hit_anything = true;
                closest_so_far = temp_rec.t;
                rec = temp_rec;
            }
        }

        return hit_anything;
    }
}

class sphere : hitable {
    public Vector3 center;
    public float radius;

    public sphere() {}
    public sphere(Vector3 cen, float r) { center = cen; radius = r; }

    public override bool hit(Ray r, float t_min, float t_max, out hit_record rec) {
        var oc = r.origin() - center;
        var a = Vector3.Dot(r.direction(), r.direction());
        var b = Vector3.Dot(oc, r.direction());
        var c = Vector3.Dot(oc, oc) - radius * radius;
        var discrement = b * b - a * c;
        if (discrement > 0) {
            var temp = (-b - Mathf.Sqrt(b * b - a * c)) / a;
            if (temp < t_max && temp > t_min) {
                rec.t = temp;
                rec.p = r.point_at_parameter(rec.t);
                rec.normal = (rec.p - center) / radius;
                return true;
            }
            temp = (-b + Mathf.Sqrt(b * b - a * c)) / a;
            if (temp < t_max && temp > t_min) {
                rec.t = temp;
                rec.p = r.point_at_parameter(rec.t);
                rec.normal = (rec.p - center) / radius;
                return true;
            }
        }

        rec.normal = new Vector3();
        rec.p = new Vector3();
        rec.t = 0;

        return false;
    }
}

class camera {
    public camera() {
        lower_left_corner = new Vector3(-2, -1, -1);
        horizontal = new Vector3(4, 0, 0);
        vertical = new Vector3(0, 2, 0);
        origin = new Vector3(0, 0, 0);
    }

    public Ray get_ray(float u, float v) { return new Ray(origin, lower_left_corner + horizontal * u + vertical * v); }

    public Vector3 origin;
    public Vector3 lower_left_corner;
    public Vector3 horizontal;
    public Vector3 vertical;
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

    static private Vector3 random_in_unit_sphere() {
        var p = new Vector3();
        do {
            p = 2.0f * new Vector3(Random.value, Random.value, Random.value) - new Vector3(1, 1, 1);
        } while (p.magnitude > 1.0f);
        return p;
    }

    static private Color color(Ray r, hitable_list world) {
        hit_record rec;
        if (world.hit(r, 0.001f, float.MaxValue, out rec)) {
            var tangent = rec.p + rec.normal + random_in_unit_sphere();
            return 0.5f * color(new Ray(rec.p, tangent - rec.p), world);
        } else {
            var unit_direction = r.direction().normalized;
            var t = 0.5f * (unit_direction.y + 1.0f);
            return (1.0f - t) * (new Color(1, 1, 1)) + t * (new Color(0.5f, 0.7f, 1.0f));
        }
    }

    static private Texture2D rayTrace(Texture2D texture) {
        const int ns = 100;
        var lower_left_corner = new Vector3(-2, -1, -1);
        var world = new hitable_list {
            new sphere(new Vector3(0, 0, -1), 0.5f),
            new sphere(new Vector3(0, -100.5f, -1), 100)
        };
        var cam = new camera();
        for (var j = texture.height - 1; j >= 0; j--) {
            for (var i = 0; i < texture.width; i++) {
                var col = Color.black;
                for (var s = 0; s < ns; s++) {
                    var u = (i + Random.Range(0f, 1f - float.Epsilon)) / texture.width;
                    var v = (j + Random.Range(0f, 1f - float.Epsilon)) / texture.height;
                    var r = cam.get_ray(u, v);
                    col += color(r, world);
                }
                col /= ns;
                col = new Color(Mathf.Sqrt(col.r), Mathf.Sqrt(col.g), Mathf.Sqrt(col.b));
                texture.SetPixel(i, j, col);
            }
        }

        return texture;
    }
}
