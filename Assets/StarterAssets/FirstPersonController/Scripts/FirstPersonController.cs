using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("Верхняя граница наклона камеры, градусы. Больше наклон — камера выше и смотрит круче вниз")]
		public float TopClamp = 50.0f;
		[Tooltip("Нижняя граница наклона камеры, градусы")]
		public float BottomClamp = 40.0f;
		[Tooltip("Наклон камеры при старте: 45° — взгляд сверху вниз под сорок пять градусов")]
		public float StartPitch = 45.0f;
		[Tooltip("Базовый азимут камеры: 225° — камера стоит по диагонали между осями X и Z, напротив направления 45°")]
		public float CameraYawBase = 225.0f;
		[Tooltip("Допуск поворота по горизонтали, градусы в обе стороны от базового азимута")]
		public float CameraYawTolerance = 10.0f;

		// cinemachine
		private float _cinemachineTargetPitch;
		private float _cameraYaw;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _fallTimeoutDelta;

	
#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			// reset our timeout on start
			_fallTimeoutDelta = FallTimeout;

			// Камера стартует в своём коридоре: базовый азимут и наклон 45°.
			// Без этого игрок при первом же движении мыши «прыгнул» бы из текущего
			// поворота объекта в базовый.
			CameraYawBase = NormalizeAngle(CameraYawBase);
			RestoreCameraCorridor();
		}

		private void Update()
		{
			// Заправка (и другие сценки) замораживают персонажа на месте: игрок стоит
			// у колонки, пока идёт отпуск топлива, и не может уйти или отъехать камерой.
			// Сам ввод не трогаем — его пишет PlayerInput каждый кадр; заморозка живёт
			// внутри Move() и LateUpdate(), иначе два источника правды спорили бы за _input.
			ApplyGravity();
			GroundedCheck();
			Move();
		}

		private void LateUpdate()
		{
			if (PlayerControlLock.IsLocked)
			{
				return;
			}

			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// Вращение ограничено: камера стоит по диагонали между осями X и Z,
			// и мышь двигает её только в пределах небольшого допуска в обе стороны.
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// Азимут копим отдельно и зажимаем в коридоре вокруг базового угла.
				// Считаем через Mathf.DeltaAngle: прямой Clamp по градусам ломается,
				// когда коридор переходит через ±180 (например, база 180°).
				float yawOffset = Mathf.DeltaAngle(CameraYawBase, _cameraYaw + _rotationVelocity);
				yawOffset = Mathf.Clamp(yawOffset, -CameraYawTolerance, CameraYawTolerance);
				_cameraYaw = NormalizeAngle(CameraYawBase + yawOffset);

				// rotate the player left and right
				transform.rotation = Quaternion.Euler(0.0f, _cameraYaw, 0.0f);
			}
		}

		/// <summary>
		/// Состояние камеры, которое нужно пережить смену сцены: диагональ, смещение
		/// внутри коридора и наклон. Без этого после возврата камера смотрела бы
		/// туда, куда указывает точка спавна, а не туда, куда игрок её повернул.
		/// </summary>
		public readonly struct CameraFacing
		{
			public readonly float BaseYaw;
			public readonly float YawOffset;
			public readonly float Pitch;

			public CameraFacing(float baseYaw, float yawOffset, float pitch)
			{
				BaseYaw = baseYaw;
				YawOffset = yawOffset;
				Pitch = pitch;
			}
		}

		/// <summary>Снимок текущего положения камеры для восстановления после смены сцены.</summary>
		public CameraFacing CaptureFacing()
		{
			return new CameraFacing(CameraYawBase, Mathf.DeltaAngle(CameraYawBase, _cameraYaw), _cinemachineTargetPitch);
		}

		/// <summary>Восстанавливает снимок камеры, сделанный до перехода в другую сцену.</summary>
		public void ApplyFacing(CameraFacing facing)
		{
			CameraYawBase = NormalizeAngle(facing.BaseYaw);
			_cameraYaw = NormalizeAngle(CameraYawBase + facing.YawOffset);
			transform.rotation = Quaternion.Euler(0.0f, _cameraYaw, 0.0f);

			if (CinemachineCameraTarget != null)
			{
				_cinemachineTargetPitch = ClampAngle(facing.Pitch, BottomClamp, TopClamp);
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
			}
		}

		/// <summary>
		/// Разворачивает коридор камеры на ближайшую диагональ к желаемому направлению
		/// и сразу ставит игрока в середину коридора. После перехода между сценами
		/// точка спавна задаёт, куда смотреть, но камера обязана остаться между осями X и Z.
		/// </summary>
		public void SetFacingYaw(float yaw)
		{
			// Диагонали — это 45°, 135°, 225°, 315°. Берём ту, что ближе к желаемому углу.
			float nearest = Mathf.Round((NormalizeAngle(yaw) - 45f) / 90f) * 90f + 45f;
			CameraYawBase = NormalizeAngle(nearest);
			RestoreCameraCorridor();
		}

		/// <summary>
		/// Возвращает камеру в середину её коридора и на стартовый наклон, не меняя сам коридор.
		/// Вызывается после телепорта: точка спавна задаёт только позицию, а направление
		/// всегда остаётся диагональю между осями X и Z, иначе камера «прыгает» на первом движении мыши.
		/// </summary>
		public void RestoreCameraCorridor()
		{
			_cameraYaw = CameraYawBase;
			transform.rotation = Quaternion.Euler(0.0f, _cameraYaw, 0.0f);

			if (CinemachineCameraTarget != null)
			{
				_cinemachineTargetPitch = ClampAngle(StartPitch, BottomClamp, TopClamp);
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);
			}
		}

		/// <summary>Приводит угол к диапазону −180…180, чтобы зажим работал и после нескольких оборотов.</summary>
		private static float NormalizeAngle(float angle)
		{
			angle %= 360f;
			if (angle > 180f) angle -= 360f;
			if (angle < -180f) angle += 360f;
			return angle;
		}

		private void Move()
		{
			// Бега и ускорения нет: единственная скорость — MoveSpeed, Shift игнорируется.
			float targetSpeed = MoveSpeed;

			// Заморозка на время заправки: вход игнорируется, персонаж гасит скорость
			// и остаётся стоять там, где начал заправку.
			bool frozen = PlayerControlLock.IsLocked;
			Vector2 moveInput = frozen ? Vector2.zero : _input.move;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (moveInput == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? moveInput.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (moveInput != Vector2.zero)
			{
				// move
				inputDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		// Прыжка нет: персонаж только ходит. Гравитация остаётся, чтобы не улетать с краёв
		// и корректно спускаться по склонам, но вверх игрок не поднимается никогда.
		private void ApplyGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}
			}
			else
			{
				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}