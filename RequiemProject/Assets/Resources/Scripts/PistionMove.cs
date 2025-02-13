﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PistionMove : MonoBehaviour
{

    public Vector3 startPosition;
    public Vector3 direction = Vector3.up;
    public bool useInitialPosition = true;
    public float verticalRange = 50;
    public float speed = 1;
    public float offset = 0;

    // Start is called before the first frame update
    void Start()
    {
        if(useInitialPosition)
        startPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {




        Vector3 pos1;
        Vector3 pos2;

        pos1 = startPosition + direction * verticalRange;
        pos2 = startPosition - direction * verticalRange;


        transform.position = Vector3.Lerp(pos1, pos2, (Mathf.Sin((Time.time + offset) * speed) + 1) / 2);


    }
}
