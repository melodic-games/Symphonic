﻿using UnityEngine;
using System.Collections.Generic;
using SymBehaviourModule;
using CharacterControl;

[SelectionBase]
public class BaseCharacterController : MonoBehaviour
{
    
    public StateMachine<BaseCharacterController> stateMachine { get; set; }

    [Header("References")]
    //References
    public Rigidbody rb;
    public CapsuleCollider capsuleCollider;   
    public Animator animator;
    public PhysicMaterial phyMat;
    public Transform characterAnchorPoint;
    public Object shockwavePrefab;
    public ParticleSystem chargeEffectParticles;

    [Header("Character")]
    //Character   
    public float playerHeight = 1.8f;
    public float colliderRadius = .25f;
    public Vector3 surfaceNormal;
    public float jumpPower = 10;
    public float topSpeed = 10;
    public float energyLevel = 0f;
    public bool chargingEnergy = false;
    public float angularAccelerationBase = 10;
   

    [Header("State")]
    //State
    public bool grounded = false;
    public bool flightEnabled = false;

    [Header("Generic")]
    //Generic
    public bool turningAround = false;
    public float turnScale = 0;
    public float turnCoefficient = 5;

    [Header("Boosting")]
    //Boosting
    public bool boosting = false;
    public float boostSpeed = 50;
    public float boostTime = 0;
    public float boostInterupt = .05f;
    public float boostWindup = .05f;

    [Header("Rotation Tensor")]
    //Rotation Tensor
    public Tensor3 orientationTensor;
    public Tensor3 targetOrientationTensor;

    [Header("Ground State")]
    //Ground State
    public float groundAccelerationBase = 50;
    public float groundTopSpeed = 100;
    public float groundWalkSpeed = 10;
    public float groundRunSpeed = 20;
    public float wallRun = 0;

    [Header("Ground Raycast")]
    //Ground Raycast
    public bool groundIsBelow;
    public RaycastHit groundHit;

    [Header("CoyoteTime")]
    //CoyoteTime
    public float coyoteTime = 0;
    public float coyoteTimeMax = .2f;

    [Header("Landing")]
    //Landing
    public float landingType = 0;
    public float impactLag = 0;

    [Header("Flight State")]
    //Flight State
    public float flightAccelerationBase = 10;
    public float flightTopSpeed = 200;
    public float spinUp = 0;
    public float lateralAirDrag = 0;
    public float backwardsAirDrag = 0;

    [Header("Physics")]
    //Physics   
    public float rbVelocityMagnitude;
    public Vector3 rbVelocityNormalized;
    public float surfaceVerticalSpeed;
    public float surfaceTraversalSpeed;
    public Vector3 localAngularVelocity;
    public bool intangible = false;

    [Header("Gravity")]
    //Gravity
    public float enableGravity;
    public GravityManager gravityManager;
    public Vector3 gravity = Vector3.down;   
    public Vector3 gravityRaw = Vector3.down;

    [Header("Control Source ")]
    //Control Source 
    public CharacterControlSource<BaseCharacterController> controlSource = new CharacterUserControl();

    [Header("Input")]
    //Input
    public bool muteInput = false;
    
    public float horizontalInput;
    public float verticalInput;

    public float virtualHorizontalInput;
    public float virtualVerticalInput;

    public float weightedPitchInput;
    public float weightedRollInput;

    public Vector3 moveDirection;
    public Vector3 moveDirectionRaw;
    public float moveDirectionMag;

    public bool crouching = false;
    public bool canRun = false;

    public float boostBuffer = Mathf.Infinity;
    public float boostBufferMax = .5f;

    public float jumpBuffer = Mathf.Infinity;
    public float jumpBufferMax = .5f;

    public float thrustInput = 0;   
    public bool focusInput = false;

    [Header("IK")]
    //IK 
    public DynamicBone[] hairDynamicBones = new DynamicBone[] { null, null };
    public DynamicBone[] bodyDynamicBones = new DynamicBone[] { null, null };

    public Vector3 characterLookAtPosition;
    private Vector3 lookDir;
    private Transform headBone;

    public Vector4 lookWeights = new Vector4(.5f, 1f, .22f, 0f);

    public bool useFootIK = false;

    private float leftFootWeight = 1;
    private float rightFootWeight = 1;
    private Vector3 leftFootPosition;
    private Vector3 rightFootPosition;


    private void Start()
    {              
        animator = GetComponentInChildren<Animator>();
        
        gameObject.layer = 10;
        gameObject.tag = "Player";

        animator = GetComponentInChildren<Animator>();
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Characters/Symphonic/Animation/SymphonicController");

        InitilizeDynamicBone();

        StartEffectControl();
     
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.angularDrag = 5;
        rb.drag = 0.1f;
        rb.centerOfMass = new Vector3(0, playerHeight / 2, 0);
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 40;

        capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        capsuleCollider.radius = .25f;
        capsuleCollider.height = playerHeight;
        capsuleCollider.center = new Vector3(0, playerHeight / 2, 0);
        phyMat = Resources.Load<PhysicMaterial>("Characters/Symphonic/PhyMatSymphonic");
        capsuleCollider.material = phyMat;

        gravityManager = FindObjectOfType<GravityManager>();

        CreateStateMachine();
    }

    private void CreateStateMachine()
    {
        stateMachine = new StateMachine<BaseCharacterController>(this);
        stateMachine.ChangeModule(SymGround.Instance);
    }

    public void ResetCurrentModule()
    {
        stateMachine.ChangeModule(SymGround.Instance);
    }

    private void Update()
    {
        if (stateMachine == null)
        {
            print("No StateMachine");
            CreateStateMachine();
        }

        //Aquire Input
        {
            controlSource.CollectInput();

            //Directional Input            
            horizontalInput = controlSource.horizontalInput;
            verticalInput = controlSource.verticalInput;
                 
            //Crouching           
            crouching = controlSource.crouching;
            
            //Jump
            if (controlSource.jump)            
                jumpBuffer = 0;
            

            //Boost    
            if (controlSource.boost && !muteInput)            
                boostBuffer = 0;
            

            //Run
            if (controlSource.canRun)            
                canRun = true;
            

            thrustInput = controlSource.thrustInput;
            focusInput = controlSource.focusInput;

            weightedPitchInput = Mathf.Lerp(weightedPitchInput, verticalInput, Time.deltaTime * 5);
            //weightedPitchInput = Mathf.Lerp(weightedPitchInput, verticalInput, thrustInput);

            weightedRollInput = Mathf.Lerp(weightedRollInput, horizontalInput, Time.deltaTime * 5);
            //weightedRollInput = Mathf.Lerp(weightedRollInput, horizontalInput, thrustInput);

            //Reset Virtual Input
            virtualVerticalInput = 0;
            virtualHorizontalInput = 0;

        }

        //Orientation 
        if (grounded)
        {

            //Set in air orientationTensor data. Used to define what is the correct forwards and up in world space.
            orientationTensor.forward = transform.forward;     
            orientationTensor.right = transform.right;            
            orientationTensor.up = transform.up;


        }
        else
        {
            //Set in air orientationTensor data. Used to define what is the correct forwards and up in world space.

            //The character flies "upwards" from their walking orientation normally
            orientationTensor.forward = transform.up;
            //Right is still right
            orientationTensor.right = transform.right;
            //Making the up vector come from the characters back
            orientationTensor.up = -transform.forward;
        }

        //Compute target tensor
        BaseCharacterFunctions.GetTargetOrientationTensor(this, orientationTensor, out targetOrientationTensor);

        UpdateEffectControl();

        UpdateDynamicBones();

        Debug.DrawLine(headBone.position, headBone.position + lookDir);

        //Wall running enabled by speed        
        wallRun = Mathf.Min(wallRun + Mathf.InverseLerp(20, 40, rbVelocityMagnitude) * Time.deltaTime, 1);
        
        //Temp- Remove later
        if (energyLevel == 1 && !grounded && (stateMachine.currentModule.GetType() != typeof(SymFlight)))
            stateMachine.ChangeModule(SymFlight.Instance);


    }

    private void LateUpdate()
    {
        stateMachine.LateUpdate();

        //Debug.DrawRay(characterAnchorPoint.position, targetForward, Color.red);
        //Debug.DrawRay(characterAnchorPoint.position, transform.forward, Color.white);
    }

    private void FixedUpdate()
    {

        //Update physics values every frame.
        {
            rbVelocityMagnitude = rb.velocity.magnitude;
            rbVelocityNormalized = rb.velocity.normalized;

            localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);

            //Record the amount of direct rotation per second for the animator
            {
                //Quaternion rotationdelta = Quaternion.Inverse(targetRotation) * behaviour.transform.rotation;

                //Vector3 angularDisplacement = rotationAxis * angleInDegrees * Mathf.Deg2Rad;
                //Vector3 kinematicAngularVelocity = angularDisplacement / Time.deltaTime;

                //owner.localKinematicAngularVelocity = owner.transform.TransformDirection(kinematicAngularVelocity);
            }

        }

        //Intangibility 
        {
            capsuleCollider.isTrigger = false;      
            
            if (Mathf.Abs(localAngularVelocity.y) > 10 && thrustInput == 1)
            {
                capsuleCollider.isTrigger = true;              
            }                     
        }

        //capsuleCollider adjustments
        {
            if (Mathf.Abs(localAngularVelocity.x) > 3)
            {
                capsuleCollider.radius = .5f;
                capsuleCollider.height = .5f;
            }

            if (Mathf.Abs(localAngularVelocity.x) < 3)
            {
                capsuleCollider.radius = .25f;
                capsuleCollider.height = playerHeight;
            }
            
        }

        //Gravity, grounded check, coyote time, and ground data grab
        {

            //Gravity Calulation    
            {
                //reset gravity disable                
                enableGravity = 1;
                
                if (gravityManager != null)                
                    gravity = gravityRaw = gravityManager.ReturnGravity(transform, transform.up * playerHeight * .5f);                
                else                
                    gravity = Vector3.Lerp(gravity, Vector3.zero, Time.deltaTime);                
            }

            //if we have exausted coyoteTime we are no longer grounded and should fall
            if (coyoteTime > coyoteTimeMax)
            {
                grounded = false;                
                coyoteTime = 0;
                wallRun = 0;
            }

            if (grounded)
            {

                //Ground check
                {
                    //direction that we check for the ground using last frames groundNormal if wall run is enabled                
                    Vector3 groundCheckDirection = Vector3.Lerp(gravity.normalized, gravity.magnitude * -surfaceNormal, wallRun).normalized;
                                                                                                            
                    groundIsBelow = Physics.Raycast(transform.position + -groundCheckDirection * playerHeight * .5f, groundCheckDirection, out groundHit, playerHeight, ~((1 << 8) | (1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore);                    
                }

                //Check to make sure we have not left the ground             
                {
                    if (groundIsBelow)
                    {
                        surfaceNormal = groundHit.normal;      

                        //Gravity redirection from wall running with new ground normal                                 
                        gravity = Vector3.Lerp(gravity, gravity.magnitude * -surfaceNormal, wallRun);    
                        
                        coyoteTime = 0;
                    }
                    else
                    {                                                                   
                        //Increment CoyoteTime 
                        coyoteTime += Time.deltaTime;

                        //CoyoteTime disables gravity while in use                
                        if (coyoteTime <= coyoteTimeMax)
                            enableGravity = 0;                        
                    }
                }                                 
            }
        }

        //Generic input muting
        { 
            muteInput = false;

            if (impactLag > 0)
                muteInput = true;
        }

        //Count down impact lag        
        {
            impactLag = Mathf.Max(0, impactLag - Time.deltaTime);
        }

        //BoostTime update
        {
            //Increment boostTime
            if (boosting)
            {
                boostTime = Mathf.Max(0, boostTime - Time.deltaTime);

                //Reset boosting and boostTime once we have boosted the full duration
                if (boostTime == 0)
                {                 
                    boosting = false;
                }
            }

        }

        //BoostBuffer Update
        {
            boostBuffer += Time.deltaTime;
        }

        //Allow the player to cancel a boost early if needed
        //if (owner.boosting && !owner.locomotionInputInterupt && owner.moveDirectionMag == 0)
        //{
        //    owner.boosting = false;
        //    owner.boostTime = 0;
        //}
        
        //jumpBuffer Update
        {
            jumpBuffer += Time.deltaTime;
        }

        //Run Locomotion Portion of Module
        {            
            stateMachine.Locomotion();
        }

        //Apply Gravity
        {
            rb.AddForce(gravity * enableGravity, ForceMode.Acceleration);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
       
        if (!grounded)
        {
            surfaceNormal = collision.GetContact(0).normal;

            //recalulate ground speeds relative to the collission surface now that surfaceNormal has updated
            surfaceVerticalSpeed = Vector3.Dot(rbVelocityMagnitude * rbVelocityNormalized, surfaceNormal);
            surfaceTraversalSpeed = rbVelocityMagnitude - Mathf.Abs(surfaceVerticalSpeed);
                     
            //Check if we should ground the character normally, or if the character should stick to walls
            if (Vector3.Dot(surfaceNormal, -gravity.normalized) > .5f)
            {
                //Reset rotation on ground on impact                
                if (Vector3.Dot(transform.up, surfaceNormal) > 0 | Mathf.Abs(localAngularVelocity.x) > 3)
                    BaseCharacterFunctions.SetRotationAroundOffset(transform, Vector3.up * playerHeight * .5f, Quaternion.LookRotation(Vector3.Cross(transform.right, -gravity), -gravity));
                else
                    BaseCharacterFunctions.SetRotationAroundOffset(transform, Vector3.up * playerHeight * .5f, Quaternion.LookRotation(Vector3.Cross(-transform.right, -gravity), -gravity));
            }
            else
            {
                wallRun = 1;
                //Reset rotation on ground on impact to stick to walls                      
                BaseCharacterFunctions.SetRotationAroundOffset(transform, Vector3.up * playerHeight * .5f, Quaternion.LookRotation(Vector3.Cross(-transform.right, surfaceNormal), surfaceNormal));        
            }

            //Reset Landing Type
            landingType = 0;

            //handle landing lag
            if (Vector3.Dot(rbVelocityNormalized, surfaceNormal) < -0.85f || crouching)
            {
                //impactLag = 1;
                //landingType = 1;
                rb.velocity = Vector3.zero;
                Debug.Log("Hault at Contact");
            }
            else
            {
                Debug.Log("Normal Contact");
            }

            if (surfaceVerticalSpeed < -20)
            {
                //CauseCameraShake(speedAlongContactNormal * .1f);
                BaseCharacterFunctions.ShockWave(collision.contacts[0].point + collision.contacts[0].normal, transform.rotation, rbVelocityMagnitude * Vector3.Dot(rbVelocityNormalized, -surfaceNormal), rb, shockwavePrefab);
            }

            
            
            //{

            //    //Character Effects
            //    {
            //        //Find Reflection Vector
            //        Vector3 reflect = Vector3.Reflect(rbVelocityNormalized, surfaceNormal);
            //        //Reflect Velocity
            //        rb.velocity = reflect * rbVelocityMagnitude;
            //        //Reflect Orientation
            //        Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.Cross(reflect, rbVelocityNormalized), reflect), surfaceNormal);
            //        BaseCharacterFunctions.SetRotationAroundOffset(transform, Vector3.up * playerHeight * .5f, rotation);
            //        //Reorient the world space angular velocity to match the rotation just applied 
            //        rb.angularVelocity = transform.TransformDirection(localAngularVelocity);
            //    }

            //    if (rbVelocityMagnitude > 20)
            //    {
            //        //cameraHelper.CameraBounce(collision);
            //        //impactLag = .5f;
            //    }

            //}

            ResetBodyDynamicBones();
        }
     
        {
            //Run Module Specific Collission 
            stateMachine.CollissionEnter(collision);
        }      

    }

    private void OnCollisionStay(Collision collision)
    {
        if (!grounded)
        {
            surfaceNormal = collision.GetContact(0).normal;

            //recalulate ground speeds relative to the collission surface now that it has updated
            surfaceVerticalSpeed = Vector3.Dot(rbVelocityNormalized * rbVelocityMagnitude, surfaceNormal);
            surfaceTraversalSpeed = rbVelocityMagnitude - Mathf.Abs(surfaceVerticalSpeed);
        }

        //Run Module Specific Collission 
        stateMachine.CollissionStay(collision);
    }

    private void InitilizeDynamicBone()
    {

        headBone = animator.GetBoneTransform(HumanBodyBones.Head);     
        lookDir = headBone.forward;

        //Initilize Dynamic Bone scripts
        {
            bodyDynamicBones[0] = animator.GetBoneTransform(HumanBodyBones.LeftShoulder).gameObject.AddComponent<DynamicBone>();

            bodyDynamicBones[0].m_Root = bodyDynamicBones[0].gameObject.transform;
            bodyDynamicBones[0].m_Exclusions = new List<Transform>() { animator.GetBoneTransform(HumanBodyBones.LeftHand) };
            bodyDynamicBones[0].m_Damping = 0.1f;
            bodyDynamicBones[0].m_Elasticity = 0.022f;
            bodyDynamicBones[0].m_Inert = 1;

            bodyDynamicBones[1] = animator.GetBoneTransform(HumanBodyBones.RightShoulder).gameObject.AddComponent<DynamicBone>();

            bodyDynamicBones[1].m_Root = bodyDynamicBones[1].gameObject.transform;
            bodyDynamicBones[1].m_Exclusions = new List<Transform>() { animator.GetBoneTransform(HumanBodyBones.RightHand) };
            bodyDynamicBones[1].m_Damping = 0.1f;
            bodyDynamicBones[1].m_Elasticity = 0.022f;
            bodyDynamicBones[1].m_Inert = 1;
        }
    }

    private void UpdateDynamicBones()
    {

        float stiff;

        stiff = Mathf.Lerp(0, 1, Mathf.InverseLerp(3, 10, Mathf.Abs(localAngularVelocity.y) + Mathf.Abs(localAngularVelocity.x)));

        foreach (DynamicBone bone in bodyDynamicBones)
        {
            if (bone == null) return;
            bone.m_Stiffness = Mathf.Lerp(bone.m_Stiffness, stiff, Time.deltaTime * 10);
            bone.m_Elasticity = Mathf.Lerp(bone.m_Elasticity, 0.022f ,Time.deltaTime * 10);           
            bone.UpdateParameters();
        }

        if (grounded)
            stiff = Vector3.Dot(rbVelocityNormalized, -transform.forward);
        else
            stiff = 0;

        foreach (DynamicBone bone in hairDynamicBones)
        {
            if (bone == null) return;
            bone.m_Stiffness = Mathf.Lerp(bone.m_Stiffness, stiff, Time.deltaTime);
            bone.UpdateParameters();
        }

       
    }

    public void ResetBodyDynamicBones()
    {
        foreach (DynamicBone bone in bodyDynamicBones)
        {
            if (bone == null) return;
            bone.m_Stiffness = 1;
            bone.m_Elasticity = 1;        
            bone.UpdateParameters();
        }
    }


    //Must be called from the OnAnimatorIK() of the game object containing the animator
    public void UpdateAnimatorLookPosition(int layerIndex)
    {
        lookDir = Vector3.Lerp(lookDir, (characterLookAtPosition - headBone.position).normalized, Time.deltaTime * 2);   
        animator.SetLookAtPosition(headBone.position + lookDir);
        animator.SetLookAtWeight(Mathf.InverseLerp(10, 0, localAngularVelocity.magnitude), lookWeights.w, lookWeights.x, lookWeights.y, lookWeights.z);
    }

    private void StartEffectControl()
    {
        chargeEffectParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = chargeEffectParticles.main;
        main.maxParticles = 100;
        main.duration = 3;
    }

    private void UpdateEffectControl()
    {

        if (chargeEffectParticles != null)
        {
            chargeEffectParticles.gameObject.transform.rotation = Quaternion.identity;
            var main = chargeEffectParticles.main;
            var shape = chargeEffectParticles.shape;
            var emission = chargeEffectParticles.emission;

            if (chargingEnergy && energyLevel != 1)
            {
                main.loop = true;
                main.startLifetime = .6f;
                main.startSpeed = -8;
                shape.radius = 5;
                emission.rateOverTime = Mathf.Lerp(0, 40, 1);//33.42f;
                if (!chargeEffectParticles.isPlaying) chargeEffectParticles.Play();
            }
            else
            {
                chargeEffectParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

    }  

}
