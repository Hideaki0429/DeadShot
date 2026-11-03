using UnityEngine;
using System.Collections;

namespace UnityChan
{
	[RequireComponent(typeof(Animator))]
	[RequireComponent(typeof(CapsuleCollider))]
	[RequireComponent(typeof(Rigidbody))]

	public class UnityChanControlScriptWithRgidBody : MonoBehaviour
	{
		// ▼追加：操作可能かどうかのフラグ。初期値はfalse（動けない）
		[Header("操作権限")]
		public bool canControl = false;

		public float animSpeed = 1.5f;
		public float lookSmoother = 3.0f;
		public bool useCurves = true;
		public float useCurvesHeight = 0.5f;

		[Header("通常時の移動速度")]
		public float forwardSpeed = 7.0f;
        [Min(0.1f)] public float runSpeed = 6f;
		public float backwardSpeed = 2.0f;

		[Header("通常時の歩行・走行アニメーション")]
		[Tooltip("WASDのみで移動しているときにLocomotionへ渡す値です。Walksのしきい値と合わせます。")]
		[Range(0.11f, 0.79f)] public float walkAnimatorSpeed = 0.2f;
		[Tooltip("Shiftを押しながら移動しているときにLocomotionへ渡す値です。Runsのしきい値と合わせます。")]
		[Range(0.8f, 1f)] public float runAnimatorSpeed = 0.8f;

		// ▼追加：銃を構えているときの専用速度設定
		[Header("銃構え（Setup）時の移動速度")]
		public float setupForwardSpeed = 3.0f;  // 通常より遅めに設定するのがおすすめ
		public float setupBackwardSpeed = 1.5f;
		[InspectorName("構え中の横移動速度")]
		[Tooltip("武器を構えてA/Dで左右移動するときの速度です。")]
		[Min(0f)]
		public float setupStrafeSpeed = 2.2f;

		public float rotateSpeed = 0.5f;
		[Header("通常時の進行方向操作")]
		[SerializeField] private float normalTurnSpeed = 420f;

		[Header("三人称カラメラ連携")]
		[SerializeField] private ThirdPersonCameraController thirdPersonCamera;
		[SerializeField] private float aimRotationSpeed = 10f;
		public float jumpPower = 3.0f;
		public GameObject Rifle;

		private CapsuleCollider col;
		private Rigidbody rb;
		private PlayerItemCollector itemCollector;
		private PlayerFlashlightController flashlight;
		private SpecialGogglesController specialGoggles;
		private AutoTargetCameraController autoTargetCamera;
		private PlayerTurnController turnController;
		private SubmachineGunController submachineGun;
		private Vector3 velocity;
		private float orgColHight;
		private Vector3 orgVectColCenter;
		private Animator anim;
		private AnimatorStateInfo currentBaseState;

		private GameObject cameraObject;

		static int idleState = Animator.StringToHash("Base Layer.Idle");
		static int locoState = Animator.StringToHash("Base Layer.Locomotion");
		static int jumpState = Animator.StringToHash("Base Layer.Jump");
		static int restState = Animator.StringToHash("Base Layer.Rest");

		public SwitchCamera switchCamera;

		private bool isSetup = false;
		private bool cPressed;
		private bool qPressed;
		private bool previousNormalMoveInput;

		[SerializeField] private float rifleShowDelay = 0.8f;
		[SerializeField] private OnKeyPress_Throw throwScript;
		[SerializeField] private FootstepAudioController footstepAudioController;

		private Coroutine rifleCoroutine;

		void Start()
		{
			anim = GetComponent<Animator>();
			col = GetComponent<CapsuleCollider>();
			rb = GetComponent<Rigidbody>();
			itemCollector = GetComponent<PlayerItemCollector>();
            flashlight = GetComponent<PlayerFlashlightController>();
			specialGoggles = GetComponent<SpecialGogglesController>();
			if (footstepAudioController == null)
				footstepAudioController = GetComponent<FootstepAudioController>();
			if (thirdPersonCamera == null)
				thirdPersonCamera = GetComponent<ThirdPersonCameraController>();
			autoTargetCamera = GetComponent<AutoTargetCameraController>();
			if (autoTargetCamera == null) autoTargetCamera = gameObject.AddComponent<AutoTargetCameraController>();
			turnController = GetComponent<PlayerTurnController>();
			if (turnController == null) turnController = gameObject.AddComponent<PlayerTurnController>();
			submachineGun = GetComponent<SubmachineGunController>();
			cameraObject = GameObject.FindWithTag("MainCamera");
			orgColHight = col.height;
			orgVectColCenter = col.center;

			// Pistol_00は射撃処理を持つ常設ルートのため無効化しない。
			if (Rifle != null) Rifle.SetActive(true);
		}

		private bool stopMovementOnAimEntry;
		private int aimEntryFrame = -1;
		private static readonly int PistolIdleState = Animator.StringToHash("Base Layer.Pistol Idle");

		public void CancelAimForDamage(bool blendToUnarmed = false)
		{
			if (anim == null) anim = GetComponent<Animator>();
			bool wasAiming = isSetup || (anim != null && anim.GetBool("Setup"));
			isSetup = false;
			stopMovementOnAimEntry = false;
			aimEntryFrame = -1;
			if (rifleCoroutine != null) { StopCoroutine(rifleCoroutine); rifleCoroutine = null; }
			if (Rifle != null) Rifle.SetActive(true);
			if (throwScript != null) throwScript.isCameraRestricted = true;
			if (anim != null)
			{
				anim.SetBool("Setup", false);
				anim.SetBool("HitWithPistol", false);
				if (blendToUnarmed && wasAiming)
				{
					anim.SetFloat("Speed", 0f);
					anim.SetFloat("Direction", 0f);
					int idle = Animator.StringToHash("Base Layer.Locomotion");
					if (anim.HasState(0, idle)) anim.CrossFadeInFixedTime(idle, .12f, 0);
				}
			}
			if (wasAiming && switchCamera != null) switchCamera.ExitSetupMode();
		}

		void Update()
		{
			// ▼追加：canControlがfalseの間は、これ以下のUpdate処理（CやQキーの判定）を行わない
			if (!canControl || !GameInputManager.Instance.Gameplay) return;

			bool takingHit = anim.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit") ||
				(anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("PlayerHit"));
			cPressed = !takingHit && GameInputManager.Instance.AimPressed &&
				(flashlight == null || !flashlight.IsSelected) &&
				(specialGoggles == null || !specialGoggles.IsShortcutSelected) &&
				!IsGrenadeShortcutSelected();
			if (cPressed)
			{
				Debug.Log("[GameInput] 構え切替");
				isSetup = !isSetup;
				Debug.Log("Setup = " + isSetup);
				anim.SetBool("Setup", isSetup);

				if (isSetup)
				{
					stopMovementOnAimEntry = true;
					aimEntryFrame = Time.frameCount;
					velocity = Vector3.zero;
					rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
					anim.SetFloat("Speed", 0f);
					anim.SetFloat("Direction", 0f);
					// Do not interrupt a damage reaction to enter the aiming pose.
					bool reacting = anim.GetCurrentAnimatorStateInfo(0).IsTag("PlayerHit") ||
						(anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsTag("PlayerHit"));
					if (!reacting && anim.HasState(0, PistolIdleState))
						anim.CrossFadeInFixedTime(PistolIdleState, .08f, 0);
					if (footstepAudioController != null)
						footstepAudioController.ReportMovementState(false, false);
					if (rifleCoroutine != null)
						StopCoroutine(rifleCoroutine);
					rifleCoroutine = StartCoroutine(ShowRifleAfterDelay());
					switchCamera.EnterSetupMode();
				}
				else
				{
					stopMovementOnAimEntry = false;
					if (rifleCoroutine != null)
						StopCoroutine(rifleCoroutine);
					if (Rifle != null) Rifle.SetActive(true);
					switchCamera.ExitSetupMode();
				}
			}

			qPressed = GameInputManager.Instance.ChangeViewPressed;
			if (qPressed)
			{
				Debug.Log("[GameInput] 視点切替");
			}

			if (qPressed)
			{
				Debug.Log("ToggleView()");
				switchCamera.ToggleView();
			}

			// 構え中はAim視点とQ視点のどちらでも射撃可能。
			if (throwScript != null)
			{
				bool isShootingView = switchCamera != null && switchCamera.IsShootingView;
				throwScript.isCameraRestricted = !isSetup || !isShootingView;
			}
		}

		bool IsGrenadeShortcutSelected()
		{
			var selection = FindObjectOfType<WeaponSelectionUI>(true);
			return selection != null && selection.SelectedWeaponId == "grenade";
		}

		void FixedUpdate()
		{
			// ローリング中の位置更新はPlayerRollControllerに任せる。
			if (PlayerRollController.Busy)
			{
				if (footstepAudioController != null) footstepAudioController.ReportMovementState(false, false);
				if (thirdPersonCamera != null) thirdPersonCamera.SetNormalMovementFollow(false);
				return;
			}
			// ▼追加：canControlがfalseの間は移動やジャンプの処理を行わない
			if (!canControl || PlayerGrenadeAnimation.BlockMovement || PlayerHealing.Busy || !GameInputManager.Instance.Gameplay || (itemCollector != null && itemCollector.IsCollecting))
			{
				if (footstepAudioController != null)
					footstepAudioController.ReportMovementState(false, false);
				if (thirdPersonCamera != null)
					thirdPersonCamera.SetNormalMovementFollow(false);
				previousNormalMoveInput = false;
                rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
				velocity = Vector3.zero;
				// 動けない間も重力だけは有効にしておく（空中に浮かないように）
				rb.useGravity = true;
				// アニメーションを待機状態(Speed=0)で固定する
				anim.SetFloat("Speed", 0);
				anim.SetFloat("Direction", 0);
				return; // ここで処理を打ち切る
			}

			if (isSetup && (stopMovementOnAimEntry || Time.frameCount == aimEntryFrame))
			{
				stopMovementOnAimEntry = false;
				velocity = Vector3.zero;
				rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
				rb.useGravity = true;
				anim.SetFloat("Speed", 0f);
				anim.SetFloat("Direction", 0f);
				return;
			}
			bool isReloading = throwScript != null && throwScript.isReloading;
			bool isGrenadeAiming = PlayerGrenadeAnimation.Busy;

			// リロード中だけ移動を禁止。構え中はA/Dで左右へ移動できる。
			float h = isReloading ? 0 : GameInputManager.Instance.Move.x;
			float v = isReloading ? 0 : GameInputManager.Instance.Move.y;

			Vector3 cameraForward = thirdPersonCamera != null
				? thirdPersonCamera.PlanarForward
				: transform.forward;
			Vector3 cameraRight = thirdPersonCamera != null
				? thirdPersonCamera.PlanarRight
				: transform.right;
			anim.speed = animSpeed;
			currentBaseState = anim.GetCurrentAnimatorStateInfo(0);
			bool isPlayingHitReaction = currentBaseState.IsTag("PlayerHit");
			if (isPlayingHitReaction)
			{
				h = 0f;
				v = 0f;
			}
			rb.useGravity = true;

			Quaternion targetRotation = rb.rotation;
			bool shouldRotate = false;

			if (isSetup && autoTargetCamera != null && autoTargetCamera.IsEnemyOrbitMovementEnabled)
			{
				Vector3 radial = autoTargetCamera.CurrentEnemy.transform.position - rb.position;
				radial.y = 0f;
				if (radial.sqrMagnitude < .001f) radial = transform.forward;
				radial.Normalize();
				Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
				Vector2 input = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
				Vector3 moveDirection = radial * input.y + tangent * input.x;
				float speed = isSetup
					? (v < -.1f ? setupBackwardSpeed : Mathf.Abs(h) > Mathf.Abs(v) ? setupStrafeSpeed : setupForwardSpeed)
					: (GameInputManager.Instance.RunHeld ? runSpeed : forwardSpeed);
				velocity = moveDirection.sqrMagnitude > .0001f ? moveDirection.normalized * speed * input.magnitude : Vector3.zero;
				anim.SetFloat("Speed", GetAimAnimatorSpeed(v));
				anim.SetFloat("Direction", h);
				targetRotation = Quaternion.LookRotation(radial, Vector3.up);
				shouldRotate = true;
				previousNormalMoveInput = false;
				if (thirdPersonCamera != null) thirdPersonCamera.SetNormalMovementFollow(false);
			}
			else if (isSetup || isGrenadeAiming)
			{
				Vector2 input = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
				Vector3 moveDirection = cameraForward * input.y + cameraRight * input.x;
				float movementSpeed = isGrenadeAiming
					? PlayerGrenadeAnimation.CurrentMovementSpeed(h, v, setupStrafeSpeed)
					: v < -0.1f
						? setupBackwardSpeed
						: Mathf.Abs(h) > Mathf.Abs(v) ? setupStrafeSpeed : setupForwardSpeed;

				velocity = moveDirection.sqrMagnitude > 0.0001f
					? moveDirection.normalized * movementSpeed * input.magnitude
					: Vector3.zero;
				anim.SetFloat("Speed", GetAimAnimatorSpeed(v));
				anim.SetFloat("Direction", h);

				if (cameraForward.sqrMagnitude > 0.0001f)
				{
					targetRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
					shouldRotate = true;
				}

				previousNormalMoveInput = false;
				if (thirdPersonCamera != null)
					thirdPersonCamera.SetNormalMovementFollow(false);
			}
			else
			{
				Vector2 input = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
				Vector3 moveDirection = cameraForward * input.y + cameraRight * input.x;
				bool hasMoveInput = moveDirection.sqrMagnitude > 0.0001f;

				if (hasMoveInput && !previousNormalMoveInput && thirdPersonCamera != null)
					thirdPersonCamera.NotifyNormalDirectionChanged();

				if (hasMoveInput)
				{
					moveDirection.Normalize();
					targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
					shouldRotate = true;

					float moveAmount = input.magnitude;
					bool isRunning = GameInputManager.Instance.RunHeld;
					velocity = moveDirection * (isRunning ? runSpeed : forwardSpeed) * moveAmount;
					// Locomotionの入力値を移動スティック量から切り離し、
					// WASDのみはWalks、Shift併用時だけRunsを再生する。
					anim.SetFloat("Speed", isRunning ? runAnimatorSpeed : walkAnimatorSpeed);
					anim.SetFloat("Direction", 0f);
				}
				else
				{
					velocity = Vector3.zero;
					anim.SetFloat("Speed", 0f);
					anim.SetFloat("Direction", 0f);
				}

				previousNormalMoveInput = hasMoveInput;
				if (thirdPersonCamera != null)
					thirdPersonCamera.SetNormalMovementFollow(hasMoveInput);
			}

			if (GameInputManager.Instance.JumpPressed)
			{
				if (currentBaseState.nameHash == locoState)
				{
					if (!anim.IsInTransition(0))
					{
						rb.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
						anim.SetBool("Jump", true);
					}
				}
			}

			// SMGのRifleIdle→RifleWalkが始まる前にRigidbodyだけ滑るのを防ぐ。
			// Speedパラメータは既にAnimatorへ渡しているため、歩行ステート開始後に自然に移動する。
			if (submachineGun != null &&
				submachineGun.ShouldHoldMovementForHeldLocomotion(new Vector2(h, v).magnitude))
			{
				velocity = Vector3.zero;
				rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
			}

			rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
			if (footstepAudioController != null)
			{
				Vector3 planarVelocity = velocity;
				planarVelocity.y = 0f;
				footstepAudioController.ReportMovementState(
					planarVelocity.sqrMagnitude > 0.0001f,
					!isSetup && GameInputManager.Instance.RunHeld);
			}

			// ターンモーション中はRoot Motionの回転を使うが、位置移動は継続する。
			if (shouldRotate && (turnController == null || !turnController.IsTurning))
			{
				if (isSetup || isGrenadeAiming)
				{
					rb.MoveRotation(Quaternion.Slerp(
						rb.rotation,
						targetRotation,
						aimRotationSpeed * Time.fixedDeltaTime
					));
				}
				else
				{
					rb.MoveRotation(Quaternion.RotateTowards(
						rb.rotation,
						targetRotation,
						normalTurnSpeed * Time.fixedDeltaTime
					));
				}
			}

			if (currentBaseState.nameHash == locoState)
			{
				if (useCurves)
				{
					resetCollider();
				}
			}
			else if (currentBaseState.nameHash == jumpState)
			{
 				if (cameraObject != null)
					cameraObject.SendMessage("setCameraPositionJumpView", SendMessageOptions.DontRequireReceiver);
				if (!anim.IsInTransition(0))
				{
					if (useCurves)
					{
						float jumpHeight = anim.GetFloat("JumpHeight");
						float gravityControl = anim.GetFloat("GravityControl");
						if (gravityControl > 0)
							rb.useGravity = false;

						Ray ray = new Ray(transform.position + Vector3.up, -Vector3.up);
						RaycastHit hitInfo = new RaycastHit();
						if (Physics.Raycast(ray, out hitInfo))
						{
							if (hitInfo.distance > useCurvesHeight)
							{
								col.height = orgColHight - jumpHeight;
								float adjCenterY = orgVectColCenter.y + jumpHeight;
								col.center = new Vector3(0, adjCenterY, 0);
							}
							else
							{
								resetCollider();
							}
						}
					}
					anim.SetBool("Jump", false);
				}
			}
			else if (currentBaseState.nameHash == idleState)
			{
				if (useCurves)
				{
					resetCollider();
				}
				if (GameInputManager.Instance.JumpPressed)
				{
					anim.SetBool("Rest", true);
				}
			}
			else if (currentBaseState.nameHash == restState)
			{
				if (!anim.IsInTransition(0))
				{
					anim.SetBool("Rest", false);
				}
			}
		}

		//void OnGUI()
		//{
		//	// ▼追加：操作不可の時はGUIの説明も非表示にする場合はこれで囲みます
		//	if (!canControl) return;

		//	GUI.Box(new Rect(Screen.width - 260, 10, 250, 150), "Interaction");
		//	GUI.Label(new Rect(Screen.width - 245, 30, 250, 30), "Up/Down Arrow : Go Forwald/Go Back");
		//	GUI.Label(new Rect(Screen.width - 245, 50, 250, 30), "Left/Right Arrow : Turn Left/Turn Right");
		//	GUI.Label(new Rect(Screen.width - 245, 70, 250, 30), "Hit Space key while Running : Jump");
		//	GUI.Label(new Rect(Screen.width - 245, 90, 250, 30), "Hit Spase key while Stopping : Rest");
		//	GUI.Label(new Rect(Screen.width - 245, 110, 250, 30), "Left Control : Front Camera");
		//	GUI.Label(new Rect(Screen.width - 245, 130, 250, 30), "Alt : LookAt Camera");
		//}

		float GetAimAnimatorSpeed(float verticalInput)
		{
			// Setup1_Locomotion は Speed=0.8 以上で Pistol Run 側へ切り替わる。
			// ピストル構え中の前進は常に PWalks 側の値を渡す。
			bool pistolAiming = isSetup && (submachineGun == null || !submachineGun.IsSelected);
			if (pistolAiming && verticalInput > .1f)
				return walkAnimatorSpeed;

			return verticalInput;
		}

		void resetCollider()
		{
			col.height = orgColHight;
			col.center = orgVectColCenter;
		}

		private IEnumerator ShowRifleAfterDelay()
		{
			yield return new WaitForSeconds(rifleShowDelay);
			var selection = FindObjectOfType<WeaponSelectionUI>(true);
			if (Rifle != null) Rifle.SetActive(true);
		}
	}
}
