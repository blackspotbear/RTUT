using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

static class Util {
    static public List<hitable> random_scene() {
        var list = new List<hitable>();

        list.Add(new hitable() {
            type = hitable.Type.Sphere,
            center = new Vector3(0, -1000, 0),
            radius = 1000,
            material = new material() {
                type = material.Type.Lambertian,
                albedo = new Color(0.5f, 0.5f, 0.5f)
            }
        });

        // can't render large number of spheres somehow :/
        //var range = 11;
        var range = 5;
        for (var a = -range; a < range; a++) {
            for (var b = -range; b < range; b++) {
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
                            fuzz = 1.0f
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

        return list;
    }
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
}

struct hitable {
    public enum Type {
        Sphere
    }
    public Type type;
    public Vector3 center;
    public float radius;
    public material material;
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

    public Vector3 origin;
    public Vector3 lower_left_corner;
    public Vector3 horizontal;
    public Vector3 vertical;
    public Vector3 u;
    public Vector3 v;
    public Vector3 w;
    public float lens_radius;
}

public class RayTracer : MonoBehaviour {
    public RawImage m_image;
    public ComputeShader computeShader;

    RenderTexture renderTexture;

    void Start() {
        renderTexture = new RenderTexture(640, 320, 0, RenderTextureFormat.ARGB32);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        rayTrace(renderTexture);
        m_image.texture = renderTexture;
    }

    void Update() {
        // nothing to do.
    }

    private void rayTrace(Texture texture) {
        Debug.Log("supportsComputeShaders = " + SystemInfo.supportsComputeShaders);

        var kernelIndex = computeShader.FindKernel("CSMain");

        computeShader.SetTexture(kernelIndex, "textureBuffer", texture);
        computeShader.SetInt("texWidth", texture.width);
        computeShader.SetInt("texHeight", texture.height);

        // hitables
        var world = Util.random_scene();
        var hitableBuffer = new ComputeBuffer(world.Count, Marshal.SizeOf(typeof(hitable)));
        hitableBuffer.SetData(world.ToArray());
        computeShader.SetBuffer(kernelIndex, "hitables", hitableBuffer);
        computeShader.SetInt("numHitable", world.Count);

        // camera
        var lookfrom = new Vector3(12, 2, 3);
        var lookat = new Vector3(0, 0.5f, 0);
        var dist_to_focus = (lookfrom - lookat).magnitude;
        var aperture = 0.1f;
        var cam = new camera(lookfrom, lookat, new Vector3(0, 1, 0), 20, (float)texture.width / texture.height, aperture, dist_to_focus);
        var camArr = new camera[1];
        camArr[0] = cam;
        var cameraBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(camera)));
        cameraBuffer.SetData(camArr);
        computeShader.SetBuffer(kernelIndex, "cameras", cameraBuffer);

        // thread sizes
        uint threadSizeX, threadSizeY, threadSizeZ;
        computeShader.GetKernelThreadGroupSizes(kernelIndex, out threadSizeX, out threadSizeY, out threadSizeZ);
        Debug.Log("thread size x, y, z = " + threadSizeX + ", " + threadSizeY + ", " + threadSizeZ);

        // run
        var sw = new System.Diagnostics.Stopwatch();
        Debug.Log("Dispatch");
        sw.Start();
        computeShader.Dispatch(
            kernelIndex,
            renderTexture.width / (int)threadSizeX,
            renderTexture.height / (int)threadSizeY,
            (int)threadSizeZ
        );
        sw.Stop();
        Debug.Log("Done. elapsed = " + sw.Elapsed);

        hitableBuffer.Dispose();
        cameraBuffer.Dispose();
    }
}
