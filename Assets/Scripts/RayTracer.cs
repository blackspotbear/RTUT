using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Jobs;

struct Xorshift {
    private uint x;
    private uint y;
    private uint z;
    private uint w;

    public ulong seed {
        set {
            x = 521288629u;
            y = (uint)(value >> 32) & 0xFFFFFFFF;
            z = (uint)(value & 0xFFFFFFFF);
            w = x ^ z;
        }
    }

    public uint valuei {
        get { return Next(); }
    }

    public float valuef {
        get { return (float)Next() / uint.MaxValue; }
    }

    private uint Next() {
        uint t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;
        w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
        return w;
    }
}

static class Util {
    static public Vector3 random_in_unit_sphere(ref Xorshift rand) {
        Vector3 p;
        do {
            p = 2.0f * new Vector3(rand.valuef, rand.valuef, rand.valuef) - new Vector3(1, 1, 1);
        } while (p.magnitude > 1.0f);
        return p;
    }

    static public Vector3 random_in_unit_disk(ref Xorshift rand) {
        Vector3 p;
        do {
            p = 2 * new Vector3(rand.valuef, rand.valuef, 0) - new Vector3(1, 1, 0);
        } while (Vector3.Dot(p, p) > 1.0);
        return p;
    }

    static public Vector3 reflect(Vector3 v, Vector3 n) {
        return v - 2 * Vector3.Dot(v, n) * n;
    }

    static public bool refract(Vector3 v, Vector3 n, float ni_over_nt, out Vector3 refracted) {
        var uv = v.normalized;
        var dt = Vector3.Dot(uv, n);
        var discriminant = 1.0f - ni_over_nt * ni_over_nt * (1.0f - dt * dt);
        if (discriminant > 0) {
            // :/ use not v but uv
            refracted = ni_over_nt * (uv - n * dt) - n * Mathf.Sqrt(discriminant);
            return true;
        }
        refracted = Vector3.zero;
        return false;
    }

    static public float schlick(float cosine, float ref_idx) {
        float r0 = (1 - ref_idx) / (1 + ref_idx);
        r0 = r0 * r0;
        return r0 + (1 - r0) * Mathf.Pow(1 - cosine, 5);
    }

    static public NativeArray<hitable> random_scene() {
        var list = new List<hitable>();

        list.Add(new hitable() {
            type = hitable.Type.Sphere,
            center = new Vector3(0, -1000, -0),
            radius = 1000,
            material = new material() {
                type = material.Type.Lambertian,
                albedo = new Color(0.5f, 0.5f, 0.5f)
            }
        });

        for (var a = -11; a < 11; a++) {
            for (var b = -11; b < 11; b++) {
                var choose_mat = Random.value;
                var center = new Vector3(a + 0.9f * Random.value, 0.2f, b + 0.9f * Random.value);
                if (choose_mat < 0.8f) { // diffuse
                    list.Add(new hitable() {
                        type = hitable.Type.Sphere,
                        center = center,
                        radius = 0.2f,
                        material = new material() {
                            type = material.Type.Lambertian,
                            albedo = new Color(Random.value * Random.value, Random.value * Random.value, Random.value * Random.value)
                        }
                    });

                } else if (choose_mat < 0.95f) { // metal
                    list.Add(new hitable() {
                        type = hitable.Type.Sphere,
                        center = center,
                        radius = 0.2f,
                        material = new material() {
                            type = material.Type.Metal,
                            albedo = new Color(0.5f * (Random.value + 1), .5f * (Random.value + 1), .5f * (Random.value + 1)),
                            fuzz = 1
                        }
                    });
                } else { // glass
                    list.Add(new hitable() {
                        type = hitable.Type.Sphere,
                        center = center,
                        radius = 0.2f,
                        material = new material() {
                            type = material.Type.Dielectric,
                            ref_idx = 1.5f
                        }
                    });
                }
            }
        }

        list.Add(new hitable() {
            type = hitable.Type.Sphere,
            center = new Vector3(0, 1, 0),
            radius = 1,
            material = new material() {
                type = material.Type.Dielectric,
                ref_idx = 1.5f
            }
        });

        list.Add(new hitable() {
            type = hitable.Type.Sphere,
            center = new Vector3(-4, 1, 0),
            radius = 1,
            material = new material() {
                type = material.Type.Lambertian,
                albedo = new Color(0.4f, 0.2f, 0.1f)
            }
        });

        list.Add(new hitable() {
            type = hitable.Type.Sphere,
            center = new Vector3(4, 1, 0),
            radius = 1,
            material = new material() {
                type = material.Type.Metal,
                albedo = new Color(0.7f, 0.6f, 0.5f),
                fuzz = 0f
            }
        });


        var arr = new NativeArray<hitable>(list.Count, Allocator.Persistent);
        for (var i = 0; i < list.Count; i++) {
            arr[i] = list[i];
        }

        return arr;
    }
}

struct Ray {
    public Ray(Vector3 a, Vector3 b) { A = a; B = b; }

    public Vector3 origin() { return A; }
    public Vector3 direction() { return B; }
    public Vector3 point_at_parameter(float t) { return A + B * t; }

    public Vector3 A;
    public Vector3 B;
}

struct material {
    public enum Type {
        Lambertian,
        Metal,
        Dielectric,
    }

    public Type type;
    public Color albedo; // lambertian, metal
    public float fuzz { // metal
        set { if (value < 1) this._fuzz = value; else this._fuzz = 1; }
        get { return this._fuzz; }
    }
    public float ref_idx; // dielectric

    private float _fuzz;

    public static bool scatter(Ray ray_in, hit_record rec, ref Xorshift rand, out Color attenuation, out Ray scattered) {
        switch (rec.mat.type) {
            case Type.Lambertian:
                return lambertian(ray_in, rec, ref rand, out attenuation, out scattered);
            case Type.Metal:
                return metal(ray_in, rec, ref rand, out attenuation, out scattered);
            case Type.Dielectric:
                return dielectric(ray_in, rec, ref rand, out attenuation, out scattered);
            default:
                return lambertian(ray_in, rec, ref rand, out attenuation, out scattered);
        }
    }

    static bool lambertian(Ray ray_in, hit_record rec, ref Xorshift rand, out Color attenuation, out Ray scattered) {
        var target = rec.p + rec.normal + Util.random_in_unit_sphere(ref rand);
        scattered = new Ray(rec.p, target - rec.p);
        attenuation = rec.mat.albedo;
        return true;
    }

    static bool metal(Ray ray_in, hit_record rec, ref Xorshift rand, out Color attenuation, out Ray scattered) {
        var reflected = Util.reflect(ray_in.direction().normalized, rec.normal);
        scattered = new Ray(rec.p, reflected + rec.mat.fuzz * Util.random_in_unit_sphere(ref rand));
        attenuation = rec.mat.albedo;
        return Vector3.Dot(scattered.direction(), rec.normal) > 0;
    }

    static bool dielectric(Ray r_in, hit_record rec, ref Xorshift rand, out Color attenuation, out Ray scattered) {
        Vector3 outward_normal;
        var reflected = Util.reflect(r_in.direction(), rec.normal);
        float ni_over_nt;
        attenuation = new Color(1.0f, 1.0f, 1.0f);
        Vector3 refracted;
        float reflect_prob;
        float cosine;
        if (Vector3.Dot(r_in.direction(), rec.normal) > 0.0f) {
            outward_normal = -rec.normal;
            ni_over_nt = rec.mat.ref_idx;
            cosine = rec.mat.ref_idx * Vector3.Dot(r_in.direction(), rec.normal) / r_in.direction().magnitude;
        } else {
            outward_normal = rec.normal;
            ni_over_nt = 1.0f / rec.mat.ref_idx;
            cosine = -Vector3.Dot(r_in.direction(), rec.normal) / r_in.direction().magnitude;
        }
        if (Util.refract(r_in.direction(), outward_normal, ni_over_nt, out refracted)) {
            reflect_prob = Util.schlick(cosine, rec.mat.ref_idx);
        } else {
            reflect_prob = 1;
        }
        if (rand.valuef < reflect_prob) {
            scattered = new Ray(rec.p, reflected);
        } else {
            scattered = new Ray(rec.p, refracted);
        }
        return true;
    }
}

struct hit_record {
    public float t;
    public Vector3 p;
    public Vector3 normal;
    public material mat;
}

struct hitable {
    public enum Type {
        Sphere
    }

    public Type type;
    public Vector3 center; // sphere
    public float radius; // sphere
    public material material; // sphere

    public static bool hit(Ray r, float t_min, float t_max, ref NativeArray<hitable> hitables, ref hit_record rec) {
        hit_record temp_rec = new hit_record();
        var hit_anything = false;
        var closest_so_far = t_max;
        for (var i = 0; i < hitables.Length; i++) {

            //UnityEngine.Profiling.Profiler.BeginSample("copy");
            var h = hitables[i]; // a copy occurs
            //UnityEngine.Profiling.Profiler.EndSample();

            if (hit(r, t_min, closest_so_far, ref h, ref temp_rec)) {
                hit_anything = true;
                closest_so_far = temp_rec.t;
                rec = temp_rec;
            }
        }

        return hit_anything;
    }

    static bool hit(Ray r, float t_min, float t_max, ref hitable hitable, ref hit_record rec) {
        switch (hitable.type) {
            case Type.Sphere:
                return sphere(r, t_min, t_max, ref hitable, ref rec);
            default:
                return sphere(r, t_min, t_max, ref hitable, ref rec);
        }
    }

    static bool sphere(Ray r, float t_min, float t_max, ref hitable hitable, ref hit_record rec) {
        var oc = r.origin() - hitable.center;
        var a = Vector3.Dot(r.direction(), r.direction());
        var b = Vector3.Dot(oc, r.direction());
        var c = Vector3.Dot(oc, oc) - hitable.radius * hitable.radius;
        var discrement = b * b - a * c;
        if (discrement > 0) {
            var temp = (-b - Mathf.Sqrt(b * b - a * c)) / a;
            if (temp < t_max && temp > t_min) {
                rec.t = temp;
                rec.p = r.point_at_parameter(rec.t);
                rec.normal = (rec.p - hitable.center) / hitable.radius;
                rec.mat = hitable.material;
                return true;
            }
            temp = (-b + Mathf.Sqrt(b * b - a * c)) / a;
            if (temp < t_max && temp > t_min) {
                rec.t = temp;
                rec.p = r.point_at_parameter(rec.t);
                rec.normal = (rec.p - hitable.center) / hitable.radius;
                rec.mat = hitable.material;
                return true;
            }
        }

        return false;
    }
}

struct camera {
    public camera(Vector3 lookfrom, Vector3 lookat, Vector3 vup, float vfov, float aspect, float aperture, float focus_dist) {
        lens_radius = aperture / 2;
        var theta = vfov * Mathf.PI / 180;
        var half_height = Mathf.Tan(theta / 2);
        var half_width = aspect * half_height;
        origin = lookfrom;
        w = (lookfrom - lookat).normalized;
        u = Vector3.Cross(vup, w).normalized;
        v = Vector3.Cross(w, u);
        lower_left_corner = origin - half_width * focus_dist * u - half_height * focus_dist * v - focus_dist * w;
        horizontal = 2 * half_width * focus_dist * u;
        vertical = 2 * half_height * focus_dist * v;
    }

    public Ray get_ray(float s, float t, ref Xorshift rand) {
        var rd = lens_radius * Util.random_in_unit_disk(ref rand);
        var offset = u * rd.x + v * rd.y;
        return new Ray(origin + offset, lower_left_corner + horizontal * s + vertical * t - origin - offset);
    }

    public Vector3 origin;
    public Vector3 lower_left_corner;
    public Vector3 horizontal;
    public Vector3 vertical;
    public Vector3 u, v, w;
    public float lens_radius;
}

public class RayTracer : MonoBehaviour {

    public RawImage m_image;

    void Start() {
        var startedAt = System.DateTime.Now;
        Debug.Log("Start ray tracing at " + startedAt);
        var texture = rayTrace(new Texture2D(640, 320));
        var elapsed = System.DateTime.Now - startedAt;
        Debug.Log("Finished " + elapsed);
        texture.Apply();
        m_image.texture = texture;
    }

    void Update() {
        // nothing to do.
    }

    static private Color color(Ray r, ref NativeArray<hitable> world, ref Xorshift rand, int depth) {
        hit_record rec = new hit_record();

        //UnityEngine.Profiling.Profiler.BeginSample("hit");
        var isHit = hitable.hit(r, 0.001f, float.MaxValue, ref world, ref rec);
        //UnityEngine.Profiling.Profiler.EndSample();

        if (isHit) {
            Ray scatteded;
            Color attenuation;
            if (depth < 50) {
                
                //UnityEngine.Profiling.Profiler.BeginSample("scatter");
                var s = material.scatter(r, rec, ref rand, out attenuation, out scatteded);
                //UnityEngine.Profiling.Profiler.EndSample();

                if (s) return attenuation * color(scatteded, ref world, ref rand, depth + 1);
                else return Color.black;
            } else {
                return Color.black;
            }
        } else {
            var unit_direction = r.direction().normalized;
            var t = 0.5f * (unit_direction.y + 1.0f);
            return (1.0f - t) * (new Color(1, 1, 1)) + t * (new Color(0.5f, 0.7f, 1.0f));
        }
    }

    [Unity.Burst.BurstCompile]
    struct RayTraceJob : IJobParallelFor {
        [ReadOnly]
        public int width;

        [ReadOnly]
        public int height;

        [ReadOnly]
        public camera cam;

        [ReadOnly]
        public int ns;

        [ReadOnly]
        public NativeArray<hitable> world;

        public NativeArray<Color> results;

        public void Execute(int i) {
            var rand = new Xorshift();
            rand.seed = (uint)i;

            for (var s = 0; s < ns; s++) {
                var u = (i % width + rand.valuef) / width;
                var v = (height - 1 - i / width + rand.valuef) / height;

                //UnityEngine.Profiling.Profiler.BeginSample("getRay");
                var ray = cam.get_ray(u, v, ref rand);
                //UnityEngine.Profiling.Profiler.EndSample();

                results[i] += color(ray, ref world, ref rand, 0);
            }

            var col = results[i] / ns;
            results[i] = new Color(Mathf.Sqrt(col.r), Mathf.Sqrt(col.g), Mathf.Sqrt(col.b));
        }
    }

    static private Texture2D rayTrace(Texture2D texture) {
        const int ns = 100;
        Debug.Log("ns = " + ns);

        var numPixel = texture.width * texture.height;
        var results = new NativeArray<Color>(numPixel, Allocator.Persistent);
            
        var world = Util.random_scene();
        var lookfrom = new Vector3(12, 2, 3);
        var lookat = new Vector3(0, 0.5f, 0);
        var dist_to_focus = (lookfrom - lookat).magnitude;
        var aperture = 0.1f;
        var cam = new camera(lookfrom, lookat, new Vector3(0, 1, 0), 20, (float)texture.width / texture.height, aperture, dist_to_focus);

        var rayTraceJob = new RayTraceJob() {
            width = texture.width,
            height = texture.height,
            ns = ns,
            cam = cam,
            results = results,
            world = world
        };

        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        var handle = rayTraceJob.Schedule(numPixel, 64);
        handle.Complete();

        sw.Stop();
        Debug.Log("job time = " + sw.Elapsed);

        var k = 0;
        for (var j = texture.height - 1; j >= 0; j--) {
            for (var i = 0; i < texture.width; i++) {
                texture.SetPixel(i, j, results[k]);
                k++;
            }
        }

        results.Dispose();
        world.Dispose();

        return texture;
    }
}
