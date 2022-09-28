using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using DG.Tweening;
using StarterAssets;
using EzySlice;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class BladeMode : MonoBehaviour
{
    [Header("相机视角")]
    public GameObject viewPoint; //相机跟随的点
    public Vector3 iniVPPos;//初始视角位置
    public Vector3 focusVPPos; //注视状态下的视角位置
    
    private bool canBladeMode;
    
    public ThirdPersonController PlayerController;
    private Animator anim;
    
    [Header("切割")]
    public GameObject cutPlane; //用于切割的平面
    public Transform reference;
    public LayerMask cutObjLayer;
    public Material crossMaterial;

    [Header("角度转动")]
    public float minRotaDegree;
    public float maxRotaDegree;
    public float offsetX;
    public float offsetY;

    public ParticleSystem[] particles;



    private void Start()
    {
        canBladeMode = false;
        cutPlane.SetActive(false);
        anim = GetComponent<Animator>();
        particles=cutPlane.GetComponentsInChildren<ParticleSystem>();
    }

    private void Update()
    {
        anim.SetFloat("Slice_x", Mathf.Clamp(Camera.main.transform.GetChild(0).localPosition.x+offsetX ,-1,1));
        anim.SetFloat("Slice_y", Mathf.Clamp(Camera.main.transform.GetChild(0).localPosition.y+offsetY , -1,1));
        //按下右键时，镜头拉近
        if (Input.GetMouseButtonDown(1))
        {
            Zoom(true);
        }

        //抬起右键时，镜头恢复
        if (Input.GetMouseButtonUp(1))
        {
            Zoom(false);
        }

        if (canBladeMode)
        {
            RotatePlane();
            
            if (Input.GetMouseButtonDown(0))
            {
                reference.DOComplete();
                reference.DOLocalMoveX(reference.localPosition.x * -1, .05f).SetEase(Ease.OutExpo);
                ShakeCamera();
                Slice();
            }
        }
    }


    //控制镜头
    private void Zoom(bool state)
    {
        canBladeMode = state;
        anim.SetBool("CanBlade",canBladeMode);
        Quaternion rota = viewPoint.transform.rotation;
        if (state) //处于注视阶段
        {
            cutPlane.SetActive(true);
            viewPoint.transform.DOLocalMove(focusVPPos, .07f, false); //镜头视角平缓移动到注视点
            PlayerController._canRotateCamera = false;//镜头固定
            PlayerController._canMove = false;//行动固定
            viewPoint.transform.DOLocalRotateQuaternion(Quaternion.Euler(0,0,0), .07f);
            DOVirtual.Float(Time.timeScale, .2f, .02f, SetTimeScale); //慢动作
            

        }
        else //退出注视阶段
        {
            viewPoint.transform.DOLocalMove(iniVPPos, .1f, false); //镜头旋转复原
            PlayerController._canRotateCamera = true;//镜头复原
            PlayerController._canMove = true;//行动复原
            DOVirtual.Float(Time.timeScale, 1, .02f, SetTimeScale); //慢动作恢复
            cutPlane.SetActive(false);
        }
        
        // float vig = state ? .6f : 0;
        // float chrom = state ? 1 : 0;
        // float depth = state ? 4.8f : 8;
        // float vig2 = state ? 0f : .6f;
        // float chrom2 = state ? 0 : 1;
        // float depth2 = state ? 8 : 4.8f;
        // DOVirtual.Float(chrom2, chrom, .1f, Chromatic);
        // DOVirtual.Float(vig2, vig, .1f, Vignette);
    }

    void SetTimeScale(float time)
    {
        Time.timeScale = time; //设置慢时间
    }


    
    //旋转刀平面,旋转视角
    private void RotatePlane()
    {
        // Vector2 vec=new Vector2();
        // Vector3 newMousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, offsetZ);
        // float vec_x = Camera.main.ScreenToWorldPoint(newMousePos).x - iniMousePosX;//平面的旋转只由鼠标的左右移动控制
        // Debug.Log(vec_x);
        // cutPlane.transform.eulerAngles = new Vector3(0, 0, vec_x);//平面沿z轴旋转vec_x角度
        
        transform.eulerAngles+=new Vector3(0,  Input.GetAxis("Horizontal") * 8,0); //左右移动控制人物旋转

        if (Input.GetAxis("Vertical")!=0)
        {
            float viewRange = CheckAngle(viewPoint.transform.eulerAngles.x + Input.GetAxis("Vertical") * 4);
            float midFloat=Mathf.Clamp(viewRange,minRotaDegree,maxRotaDegree); 
            // Debug.Log(midFloat);
            viewPoint.transform.eulerAngles = new Vector3(midFloat, viewPoint.transform.eulerAngles.y, 0);
        }

        cutPlane.transform.eulerAngles += new Vector3(0, 0, -Input.GetAxis("Mouse X") * 5); //平面旋转
    }

    //相机震动及粒子特效
    private void ShakeCamera()
    {
        Camera.main.GetComponent<CinemachineImpulseSource>().GenerateImpulse();
        foreach (ParticleSystem p in particles)
        {
            p.Play();
        }
    }

    //切割
    private void Slice()
    {
        //找到所有与平面碰撞的物体
        Collider[] hits = Physics.OverlapBox(cutPlane.transform.position, new Vector3(5, 0.1f, 5),
            cutPlane.transform.rotation, cutObjLayer);
        if (hits.Length<=0)
            return;

        //对每个物体切出船体并分别创建上下两个物体
        for (int i = 0; i < hits.Length; i++)
        {
            SlicedHull hull = SlicedObj(hits[i].gameObject, crossMaterial);
            if (hull!=null)
            {
                GameObject bottom = hull.CreateLowerHull(hits[i].gameObject, crossMaterial);
                GameObject top = hull.CreateUpperHull(hits[i].gameObject, crossMaterial);
                AddPropertiesToNewHull(bottom);
                AddPropertiesToNewHull(top);
                Destroy(hits[i].gameObject);
            }
        }
    }

    //传入物体和材质返回被平面切割的船体
    private SlicedHull SlicedObj(GameObject obj, Material crossMat)
    {
        //若该物体没有实体
        if (obj.GetComponent<MeshFilter>()==null)
        {
            return null;
        }
        //返回切好的船体
        return obj.Slice(cutPlane.transform.position, cutPlane.transform.up, crossMat);
    }

    //给被切割出的新物体增加属性
    private void AddPropertiesToNewHull(GameObject obj)
    {
        obj.layer = 6;  //设置layer
        Rigidbody rb = obj.AddComponent<Rigidbody>();//添加刚体
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        MeshCollider coll = obj.AddComponent<MeshCollider>();//添加碰撞体
        coll.convex = true;
        
        rb.AddExplosionForce(100,obj.transform.position,20); //加入爆炸效果

    }
    
    //
    // void Chromatic(float x)
    // {
    //     Camera.main.GetComponentInChildren<PostProcessVolume>().profile.GetSetting<ChromaticAberration>().intensity.value = x;
    // }
    //
    // void Vignette(float x)
    // {
    //     Camera.main.GetComponentInChildren<PostProcessVolume>().profile.GetSetting<Vignette>().intensity.value = x;
    // }
    //
    //
    //更改到正确的欧拉角角度
    public float CheckAngle(float value)
    {
        float angle = value - 180;

        if (angle > 0)
            return angle - 180;

        return angle + 180;
    }
    
}