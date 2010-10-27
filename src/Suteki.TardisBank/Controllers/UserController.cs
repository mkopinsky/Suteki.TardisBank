using System;
using System.Web.Mvc;
using Suteki.TardisBank.Model;
using Suteki.TardisBank.Mvc;
using Suteki.TardisBank.Services;
using Suteki.TardisBank.ViewModel;

namespace Suteki.TardisBank.Controllers
{
    public class UserController : Controller
    {
        readonly IUserService userService;
        readonly IFormsAuthenticationService formsAuthenticationService;

        public UserController(IUserService userService, IFormsAuthenticationService formsAuthenticationService)
        {
            this.userService = userService;
            this.formsAuthenticationService = formsAuthenticationService;
        }

        [ChildActionOnly]
        public ActionResult Index()
        {
            var user = userService.CurrentUser;
            var name = user == null ? "Guest" : user.Name;
            return View("Index", new UserViewModel { Name = name, IsLoggedIn = user != null });
        }

        [HttpGet]
        public ActionResult Register()
        {
            return View("Register", GetRegistrationViewModel());
        }

        static RegistrationViewModel GetRegistrationViewModel()
        {
            return new RegistrationViewModel
            {
                Email = "",
                Name = "",
                Password = ""
            };
        }

        [HttpPost, UnitOfWork]
        public ActionResult Register(RegistrationViewModel registrationViewModel)
        {
            return RegisterInternal(registrationViewModel,
                createUser: (pwd) => new Parent(registrationViewModel.Name, registrationViewModel.Email, pwd),
                confirmAction: () => RedirectToAction("Confirm"),
                invalidModelStateAction: () => View("Register", registrationViewModel),
                afterUserCreated: user => formsAuthenticationService.SetAuthCookie(user.UserName, false)
                );
        }

        ActionResult RegisterInternal(
            RegistrationViewModel registrationViewModel, 
            Func<string, User> createUser,
            Func<ActionResult> confirmAction, 
            Func<ActionResult> invalidModelStateAction,
            Action<User> afterUserCreated = null)
        {
            if (registrationViewModel == null)
            {
                throw new ArgumentNullException("registrationViewModel");
            }
            if (createUser == null)
            {
                throw new ArgumentNullException("createUser");
            }
            if (confirmAction == null)
            {
                throw new ArgumentNullException("confirmAction");
            }
            if (invalidModelStateAction == null)
            {
                throw new ArgumentNullException("invalidModelStateAction");
            }

            if (ModelState.IsValid)
            {
                var hashedPassword = formsAuthenticationService.HashAndSalt(
                    registrationViewModel.Email,
                    registrationViewModel.Password);

                var user = createUser(hashedPassword);
                userService.SaveUser(user);

                if (afterUserCreated != null)
                {
                    afterUserCreated(user);
                }
                return confirmAction();
            }

            return invalidModelStateAction();
        }

        [HttpGet]
        public ActionResult Confirm()
        {
            return View("Confirm");
        }

        [HttpGet]
        public ActionResult Login()
        {
            var loginViewModel = new LoginViewModel
            {
                Name = "",
                Password = ""
            };
            return View("Login", loginViewModel);
        }

        [HttpPost, UnitOfWork]
        public ActionResult Login(LoginViewModel loginViewModel)
        {
            if (loginViewModel == null)
            {
                throw new ArgumentNullException("loginViewModel");
            }

            if (ModelState.IsValid)
            {
                var user = userService.GetUserByUserName(loginViewModel.Name);
                if (user != null)
                {
                    var hashedPassword = formsAuthenticationService.HashAndSalt(
                        loginViewModel.Name,
                        loginViewModel.Password);

                    if(hashedPassword == user.Password)
                    {
                        formsAuthenticationService.SetAuthCookie(user.UserName, false);
                        return RedirectToAction("Index", "Home");
                    }
                    ModelState.AddModelError("Password", "Invalid Password");
                }
                else
                {
                    ModelState.AddModelError("Name", "Invalid Name");
                }
            }

            return View("Login", loginViewModel);
        }

        [HttpGet]
        public ActionResult Logout()
        {
            formsAuthenticationService.SignOut();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public ActionResult AddChild()
        {
            return View("AddChild", GetRegistrationViewModel());
        }

        [HttpPost, UnitOfWork]
        public ActionResult AddChild(RegistrationViewModel registrationViewModel)
        {
            var parent = userService.CurrentUser as Parent;
            if(parent == null)
            {
                //throw new TardisBankException("You must be a parent in order to register a Child");
                return StatusCode.NotFound;
            }

            return RegisterInternal(registrationViewModel,
                createUser: (pwd) => parent.CreateChild(registrationViewModel.Name, registrationViewModel.Email, pwd),
                confirmAction: () => RedirectToAction("Index", "Child"),
                invalidModelStateAction: () => View("AddChild", registrationViewModel)
                );
        }

        [HttpGet]
        public ActionResult Messages()
        {
            var user = userService.CurrentUser;
            if (user == null)
            {
                return StatusCode.NotFound;
            }
            return View("Messages", user);
        }

        [HttpGet, UnitOfWork]
        public ActionResult ReadMessage(int id)
        {
            var user = userService.CurrentUser;
            if (user == null)
            {
                return StatusCode.NotFound;
            }
            user.ReadMessage(id);
            return RedirectToAction("Messages");
        }
    }
}