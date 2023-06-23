using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class WeaponBase : MonoBehaviour
{
    //extra references that aren't visible in the inspector
    private CharacterMovement movement;
    private WeaponSwap swap;
    [HideInInspector]
    public bool wantsReload;
    [HideInInspector]
    public WeaponScriptableObject currentWeaponInfo;
    [HideInInspector]
    public bool canSwap = true;
    [Range(0,1),HideInInspector]
    public float scopeValue;
    [HideInInspector]
    public bool isScoping;

    //references to the positions of the weapon and other basic information that is needed for the functions of the weapon
    [Header("Base reference")]
    public Transform weapon;
    public Transform hipPos;
    public Transform scopePos;
    [HideInInspector]
    public Transform barrel;
    public LayerMask hitMask;
    public string reloadInput;
    public string scopeInput;
    public WeaponTextInputs weaponTextReference;
    public GameObject crosshair;
    public RectTransform crosshairLines;
    private Vector2 defaultcrosshairSize;
    public float spreadCrossHairIncrease;
    public AudioMixerGroup group;

    //The settings for the fov change of the camera;
    [Header("Camera")]
    public Transform cam;
    public float shootFovChange;
    public float fovChangeLength;
    public float scopeFovChange;
    private float fov;
    public float fovChangeSpeed;

    //impact particles
    /// the array of impacts are a list of the physics materials and particles, it uses the physics material to check what to use
    [Header("Particles")]
    public CollisionParticle[] impactParticles;
    public float particleLifeTime;

    //Recoil base settings of both the weapon and the camera
    ///it uses this for all the weapons
    [Header("Recoil")]
    public float recoilMultiplyValue;
    public float weaponRecoilReturnSpeed;
    public float spreadReturnSpeed;
    [Space]
    public float cameraRecoilReturnSpeed;
    public float cameraRecoilreturnApplySpeed;

    //private recoil saved data
    ///this is where the active recoil is saved so it can be applied overtime
    private Vector2 currentCameraRecoil;
    private Vector3 currentWeaponRecoil;
    private Vector2 returnToNormal;
    private float currentWeaponRotRecoil;
    [Range(0, 1)]
    private float currentWeaponSpread;

    [HideInInspector]
    public IEnumerator activeReload = null;
    [HideInInspector]
    public AudioSource soundSource;
    public AudioSource clipSoundSource;

    //Start function for base references
    ///it also starts the coroutines for the checks of the shooting, recoil, scoping and the crosshair
    public void Start()
    {
        soundSource = gameObject.AddComponent<AudioSource>();
        clipSoundSource = gameObject.AddComponent<AudioSource>();
        soundSource.outputAudioMixerGroup = group;
        clipSoundSource.outputAudioMixerGroup = group;
        defaultcrosshairSize = crosshairLines.sizeDelta;
        fov = Camera.main.fieldOfView;
        movement = GetComponent<CharacterMovement>();
        swap = GetComponent<WeaponSwap>();
        currentWeaponInfo = swap.weapons[0];
        DisplayWeaponBaseInfo();
        StartCoroutine(Shooting());
    }

    public void Update()
    {
        DisplayCrossHairLines();
        Scoping();
        Recoil();
    }

    //Displays the information of the weapon like ammo in the UI
    public void DisplayWeaponBaseInfo()
    {
        if (weaponTextReference.magazineSize == null)
            return;
        if(swap)
        {
            weaponTextReference.magazineSize.text = currentWeaponInfo.magazineSize.ToString();
            weaponTextReference.pouchAmmo.text = Manager.pouch[swap.currentWeapon].ToString();
            weaponTextReference.currentAmmo.text = swap.GetAmmoValue().ToString();
            if(weaponTextReference.weaponIcon)
                weaponTextReference.weaponIcon.sprite = currentWeaponInfo.weaponIcon;
        }
        currentWeaponSpread = currentWeaponInfo.baseSpread;
    }

    //uses the active spread to set the size of the crosshair to let the player see how much spread is active on the weapon
    public void DisplayCrossHairLines()
    {
        ///it doesn't have to change when scoped in because the crosshair is then invisible
        if (!isScoping)
        {
            crosshairLines.sizeDelta = defaultcrosshairSize + (Vector2.one * (spreadCrossHairIncrease * currentWeaponSpread));
        }
    }

    //Scope check and movement
    ///check when you start and end scoping
    ///and moves the weapon from hipfire to scoping position and the other way around
    public void Scoping()
    {
        ///checks for when you start and stop scoping
        if (Input.GetButtonDown(scopeInput) && !isScoping && swap.activeSwap == null)
        {
            fov -= scopeFovChange;
            isScoping = true;
        }
        else if (Input.GetButtonUp(scopeInput) && isScoping)
        {
            fov += scopeFovChange;
            isScoping = false;
        }
        ///sets the crosshair activity
        crosshair.SetActive(!isScoping);

        ///applies the camera fov based on when you are scoping and shooting
        Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, fov, Time.deltaTime * fovChangeSpeed);

        ///sets the scope value as check for other functions like spread to see how much it should be reduced
        if (isScoping)
            scopeValue += currentWeaponInfo.scopeSpeed * Time.deltaTime;
        else
            scopeValue -= currentWeaponInfo.scopeSpeed * Time.deltaTime;
        scopeValue = Mathf.Clamp01(scopeValue);

        ///sets the weapon position and rotation based on if you are scoping or not
        if (swap.currentInfo.changeObject)
            swap.currentInfo.changeObject.position = Vector3.Lerp(swap.currentInfo.pos1.position, swap.currentInfo.pos2.position, scopeValue);
        weapon.parent.position = Vector3.Lerp(hipPos.position, scopePos.position, scopeValue);
        weapon.parent.rotation = Quaternion.Lerp(hipPos.rotation, scopePos.rotation, scopeValue);
    }

    public IEnumerator Reload()
    {
        if(swap.currentInfo.weaponAnimator)
            swap.currentInfo.weaponAnimator.SetTrigger("Reload");
        if (currentWeaponInfo.perBulletReload)
        {
            yield return new WaitForSeconds(currentWeaponInfo.beginEndTime.x);
            int reloadAmount = currentWeaponInfo.magazineSize - Manager.currentAmmo[swap.currentWeapon];
            swap.currentInfo.weaponAnimator.SetBool("Reloading", true);
            bool end = false;
            for (int i = 0; i < reloadAmount; i++)
            {
                float intervalTime = currentWeaponInfo.reloadTime;
                while (intervalTime > 0)
                {
                    if (!end && Input.GetButtonDown("Fire1"))
                    {
                        end = true;
                    }
                    intervalTime -= Time.deltaTime;
                    yield return null;
                }
                AudioManager.PlayAudioClip(currentWeaponInfo.reloadSound, soundSource, group, true, false);
                if (!swap.Reload(true) || end)
                    break;
            }
            swap.currentInfo.weaponAnimator.SetBool("Reloading", false);
            yield return new WaitForSeconds(currentWeaponInfo.beginEndTime.y / 2f);
            AudioManager.PlayAudioClip(swap.currentInfo.endSound, soundSource, group, false, false);
            yield return new WaitForSeconds(currentWeaponInfo.beginEndTime.y / 2f);
        }
        else
        {
            AudioManager.PlayAudioClip(currentWeaponInfo.reloadSound, soundSource, group, false, false);
            yield return new WaitForSeconds(currentWeaponInfo.reloadTime);
            swap.Reload();
        }
        wantsReload = false;
        if (weaponTextReference.currentAmmo != null)
            weaponTextReference.currentAmmo.text = swap.GetAmmoValue().ToString();
        activeReload = null;
    }

    //Shooting
    ///This is the base check of if you can shoot and if you have burst-fire, automatic-fire or just single-fire
    ///It also check for the reload
    public IEnumerator Shooting()
    {
        if(CharacterMovement.playerObject.health <= 0)
        {
            StopAllCoroutines();
        }

        canSwap = true;
        ///makes sure it doesn't mess up if the games has a freeze frame when loading in data
        if (Time.deltaTime < 1f && swap.activeSwap == null && !Manager.isPauzed)
        {
            ///calculates what type of fireType you use for easy reference
            ///0: singlefire / 1: burstFire / 2: autoFire
            int fireType = 0;
            canSwap = false;
            switch(currentWeaponInfo.fireType)
            {
                case WeaponScriptableObject.FireType.burst:
                    fireType = 1;
                    break;
                case WeaponScriptableObject.FireType.auto:
                    fireType = 2;
                    break;
            }

            ///check is you want to reload and if you are able to reload
            if ((Input.GetButtonDown(reloadInput) || wantsReload) && swap.GetAmmoValue() != currentWeaponInfo.magazineSize && !isScoping && Manager.pouch[swap.currentWeapon] > 0 && activeReload == null)
            {
                activeReload = Reload();
                StartCoroutine(activeReload);
            }
            ///check if you are in auto fire or not and then checks if you are holding the fire button or just pressing it
            if ((fireType == 2 && Input.GetButton("Fire1") || fireType != 2 && Input.GetButtonDown("Fire1")) && activeReload == null)
            {
                ///ammo check
                if(swap.GetAmmoValue() > 0)
                {
                    /// check if you are in burst and then changes the amount of bullets it should shoot after eachother
                    int fireAmount = fireType == 1? currentWeaponInfo.burstAmount : 1;
                    while(fireAmount != 0)
                    {
                        /// calls the Fire function
                        Fire();
                        ///Adds the total spread count so the next bullet will be less straigth
                        if (currentWeaponSpread <= currentWeaponInfo.spread)
                        {
                            if (currentWeaponSpread + currentWeaponInfo.spreadIncrease > currentWeaponInfo.spread)
                                currentWeaponSpread = currentWeaponInfo.spread;
                            else
                                currentWeaponSpread += currentWeaponInfo.spreadIncrease;
                        }
                        fireAmount--;
                        ///applies the fov change for shooting
                        fov += shootFovChange;
                        yield return new WaitForSeconds(fovChangeLength);
                        fov -= shootFovChange;
                        ///timer if it needs to fire another round
                        if(fireAmount != 0)
                        {
                            yield return new WaitForSeconds(currentWeaponInfo.burstDelay - fovChangeLength);
                        }
                    }
                    ///start a delay for in between shots
                    if(swap.currentInfo.hasPump)
                        swap.currentInfo.weaponAnimator.SetTrigger("Pump");
                    float value = currentWeaponInfo.fireDelay - fovChangeLength;
                    while (value > 0)
                    {
                        value -= Time.deltaTime;
                        ///sets a bool to active if you want to reload in the time you have to wait
                        if (Input.GetButtonDown(reloadInput))
                            wantsReload = true;
                        yield return null;
                    }
                }
                else if (Input.GetButtonDown("Fire1"))
                {
                    wantsReload = true;
                }
            }
            canSwap = true;
        }
        yield return null;
        StartCoroutine(Shooting());
    }

    //Recoil of the weapon and camera
    ///makes it so the recoil isn't instant and more overtime so it feels better
    public void Recoil()
    {
        ///returns the active spread to the default value
        if (currentWeaponSpread > currentWeaponInfo.baseSpread)
            currentWeaponSpread -= Time.deltaTime * spreadReturnSpeed;
        ///calculates the direction where the weapon needs to move to
        Vector3 recoilDirection = Vector3.zero;
        recoilDirection += -weapon.parent.forward * currentWeaponRecoil.z;
        recoilDirection += weapon.parent.up * currentWeaponRecoil.y;
        ///applies the weapon recoil movement and rotation
        weapon.transform.Translate(recoilDirection * Time.deltaTime, Space.World);
        weapon.transform.Rotate(Vector3.left * currentWeaponRotRecoil * Time.deltaTime);
        weapon.transform.Rotate(Vector3.up * currentWeaponRecoil.x * Time.deltaTime);
        ///calls the camera recoil
        movement.CameraRotation(currentCameraRecoil.x, currentCameraRecoil.y);
        returnToNormal += currentCameraRecoil * Time.deltaTime;
        ///returns the recoil values to zero
        currentWeaponRotRecoil = Mathf.Lerp(currentWeaponRotRecoil, 0, Time.deltaTime * weaponRecoilReturnSpeed);
        currentWeaponRecoil = Vector3.Lerp(currentWeaponRecoil, Vector3.zero, Time.deltaTime * weaponRecoilReturnSpeed);
        currentCameraRecoil = Vector2.Lerp(currentCameraRecoil, Vector2.zero, Time.deltaTime * cameraRecoilReturnSpeed);

        //recoil return X value
        ///makes it so when you move into the recoil it won't still be applied
        if (Mathf.Abs(returnToNormal.x) > 0.02f)
        {
            float returnDevider = Vector2.Distance(currentCameraRecoil, Vector2.zero) > 1 ? Vector2.Distance(currentCameraRecoil, Vector2.zero) : 1;
            int direction = returnToNormal.x < 0 ? -1 : 1;
            float calculateValue = Mathf.Abs(returnToNormal.x);

            if (Input.GetAxis("Mouse X") != 0)
            {
                int mouseDirection = Input.GetAxis("Mouse X") < 0 ? -1 : 1;
                if (mouseDirection != direction)
                {
                    returnToNormal.x += Input.GetAxis("Mouse X") * movement.rotateSpeed * Time.deltaTime;
                }
            }
            ///returns the camera to normal
            returnToNormal.x -= (cameraRecoilreturnApplySpeed * Time.deltaTime * calculateValue * direction) / returnDevider;
            movement.CameraRotation((-cameraRecoilreturnApplySpeed * calculateValue * direction) / returnDevider, 0);
        }
        //recoil return Y value
        ///makes it so when you move into the recoil it won't still be applied
        if (returnToNormal.y > 0)
        {
            float returnDevider = Vector2.Distance(currentCameraRecoil, Vector2.zero) > 1 ? Vector2.Distance(currentCameraRecoil, Vector2.zero) : 1;
            returnToNormal.y -= (cameraRecoilreturnApplySpeed * Time.deltaTime * returnToNormal.y) / returnDevider;
            if (Input.GetAxis("Mouse Y") < 0)
            {
                returnToNormal.y += Input.GetAxis("Mouse Y") * movement.rotateSpeed * Time.deltaTime;
            }
            ///returns the camera to normal
            movement.CameraRotation(0, (-cameraRecoilreturnApplySpeed * returnToNormal.y) / returnDevider);
        }
    }

    //function to fire the bullet
    ///also shoots multiple bullets in case it is a shotgun
    public void Fire()
    {
        ///check the ammo value
        if(swap.GetAmmoValue() <= 0)
        {
            Debug.Log("No Ammo");
            return;
        }
        ///lowers the ammo count
        if(swap.currentInfo.muzzleFlash)
            Destroy(Instantiate(swap.currentInfo.muzzleFlash, barrel.position, barrel.rotation, barrel), swap.currentInfo.destroyTime);
        swap.RemoveAmmo();
        if (weaponTextReference.currentAmmo != null)
            weaponTextReference.currentAmmo.text = swap.GetAmmoValue().ToString();
        ///plays the fire sound
        AudioManager.PlayAudioClip(currentWeaponInfo.fireSound, soundSource, group, true, false);
        /*if(currentWeaponInfo.emptyClipSound != null)
        {
            float clipVolume = currentWeaponInfo.clipSoundCurve.Evaluate(1 - (1f / currentWeaponInfo.magazineSize) * swap.GetAmmoValue());
            clipSoundSource.volume = clipVolume;
            AudioManager.PlayAudioClip(currentWeaponInfo.emptyClipSound, clipSoundSource, group, clipVolume, false);
        }*/

        ///sets the amount of bullets that need to be fired
        int value = currentWeaponInfo.bulletAmount;
        Vector3 spread = new Vector3();

        ///loops through the bullet count to fire multiple bullets
        while(value > 0)
        {
            ///sets the spread of the bullet
            spread = new Vector3(Random.Range(1f, -1f), Random.Range(1f, -1f), Random.Range(1f, -1f));
            if(Vector3.Distance(spread,Vector3.zero) > 1)
                spread.Normalize();
            spread *= currentWeaponSpread * Mathf.Lerp(1, currentWeaponInfo.scopeValueChange, scopeValue);

            ///checks if it needs to spawn a projectile or fire a raycast
            if (currentWeaponInfo.projectileType == WeaponScriptableObject.ProjectileType.projectile)
            {
                ///spawns a projectile and sets the spread and information
                GameObject projectile = Instantiate(currentWeaponInfo.projectile, barrel.position, cam.rotation);
                projectile.transform.Rotate(spread);
                projectile.GetComponent<ProjectileBase>().Launch(currentWeaponInfo.projectileLaunchStrength, currentWeaponInfo.damage, currentWeaponInfo.projectileBounces, currentWeaponInfo.projectileLifeTime, currentWeaponInfo.forceMultiply);
            }
            else
            {
                ///calls the raycast funcion
                ShootRayCast(spread);
            }
            value--;
        }
        ///applies the weapon and camera recoil
        float weaponX = currentWeaponRecoil.x;
        currentWeaponRecoil = currentWeaponInfo.weaponRecoil * Mathf.Lerp(1, currentWeaponInfo.scopeValueChange, scopeValue);
        currentWeaponRotRecoil = currentWeaponInfo.weaponRotationalRecoil * Mathf.Lerp(1, currentWeaponInfo.scopeValueChange, scopeValue);
        currentWeaponRecoil *= recoilMultiplyValue;
        currentWeaponRotRecoil *= recoilMultiplyValue;

        ///sets a multiply value for if the recoil should be left or right
        int multiplyValue = (Random.Range(0, 2) == 1) ? 1 : -1;

        ///sets xvalue of the recoil
        float xValue = Random.Range(currentWeaponInfo.cameraMinRecoil.x, currentWeaponInfo.cameraMaxRecoil.x) * multiplyValue;
        currentWeaponRecoil.x = (weaponX + (currentWeaponRecoil.x * Mathf.Clamp(multiplyValue, -1f, 1f) * Random.Range(0f, 1f))) * Mathf.Lerp(1, currentWeaponInfo.scopeValueChange, scopeValue);
        currentCameraRecoil = new Vector2(xValue, Random.Range(currentWeaponInfo.cameraMinRecoil.y, currentWeaponInfo.cameraMaxRecoil.y)) * Mathf.Lerp(1, currentWeaponInfo.scopeValueChange, scopeValue);
    }

    //Raycast bullet function
    public void ShootRayCast(Vector3 spread)
    {
        ///sets the direction the raycast should fire in
        Vector3 direction = cam.forward;
        direction = Quaternion.Euler(spread) * direction;

        RaycastHit hit = new RaycastHit();
        if(Physics.Raycast(cam.position,direction,out hit, currentWeaponInfo.raycastLength, hitMask))
        {
            ///checks what type of bullet you use for the impact
            switch (currentWeaponInfo.bulletType)
            {
                ///normal bullet: applies damage when hitting an enemy
                case WeaponScriptableObject.BulletType.normal:
                    hit.transform.GetComponent<EnemyHitPoint>()?.TakeDamage(currentWeaponInfo.damage, currentWeaponInfo.forceMultiply, direction, hit.point);
                    PhysicMaterial impactMaterial = hit.transform.GetComponent<Collider>().material;
                    ///checks the physics material of the hit collider to spawn the right impact particle
                    foreach(CollisionParticle set in impactParticles)
                    {
                        if(set.material && set.material.name + " (Instance)" == impactMaterial.name)
                        {
                            GameObject particle = Instantiate(set.particle, hit.point, Quaternion.identity);
                            particle.transform.LookAt(hit.point + hit.normal);
                            Destroy(particle, particleLifeTime);
                            return;
                        }
                    }
                    break;
                ///explosive rounds: spawns an explosion particle on the impact point and applies damage to enemies around the impact point thats based on the distance to the impact point
                case WeaponScriptableObject.BulletType.explosive:
                    GameObject explosion = Instantiate(currentWeaponInfo.explosionParticle, hit.point, Quaternion.identity);
                    Destroy(explosion, particleLifeTime);
                    ///applies the damage
                    Collider[] collisions = Physics.OverlapSphere(hit.point, currentWeaponInfo.explosiveRadius, hitMask);
                    foreach(Collider col in collisions)
                    {
                        col.GetComponent<EnemyHitPoint>()?.TakeDamage(currentWeaponInfo.damage, currentWeaponInfo.forceMultiply, col.transform.position - hit.point, hit.point);
                    }

                    break;
            }

            ///spawns a default particle in case there wasn't a correct particle that was found
            GameObject reserveParticle = Instantiate(impactParticles[0].particle, hit.point, Quaternion.identity);
            reserveParticle.transform.LookAt(hit.point + hit.normal);
            Destroy(reserveParticle, particleLifeTime);
            return;
        }
    }

    //Constructors for references
    ///impact particle info
    [System.Serializable]
    public class CollisionParticle
    {
        public PhysicMaterial material;
        public GameObject particle;
    }
    ///ui text references
    [System.Serializable]
    public class WeaponTextInputs
    {
        public Text currentAmmo, magazineSize, pouchAmmo;
        public Image weaponIcon;
    }
}
