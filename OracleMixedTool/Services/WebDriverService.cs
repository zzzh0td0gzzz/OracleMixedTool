﻿using ChromeDriverLibrary;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OracleMixedTool.Forms;
using OracleMixedTool.Models;
using SeleniumUndetectedChromeDriver;

namespace OracleMixedTool.Services
{
    public class WebDriverService
    {
        private static readonly object _lockClipboard = new();
        public static int DefaultTimeout { get; set; } = 30;
        public static int DefaultWaitChangePass { get; set; } = 60;
        public static int DefaultWaitDelete { get; set; } = 60;
        public static int DefaultWaitReboot { get; set; } = 60;
        public static int DefaultWaitCreateNew { get; set; } = 60;

        public static async Task<Tuple<bool, string>> EnterTenant(UndetectedChromeDriver driver, Account account, CancellationToken token)
        {
            var step = "GoToUrl";
            try
            {
                driver.GoToUrl("https://www.oracle.com/cloud/sign-in.html");
                await Task.Delay(3000, token).ConfigureAwait(false);

                step = "HandleCookieAccept";
                await HandleCookieAccept(driver, token).ConfigureAwait(false);

                step = "cloudAccountName";
                driver.Sendkeys("#cloudAccountName", account.Email.Split('@')[0], true, DefaultTimeout, token);
                await Task.Delay(1000, token).ConfigureAwait(false);

                step = "cloudAccountButton";
                driver.Click("#cloudAccountButton", DefaultTimeout, token);
                await Task.Delay(DefaultTimeout * 1000, token);

                if (driver.Url.Contains("oraclecloud.com/v1/oauth2/authorize"))
                {
                    step = "submitFederationBtn";
                    driver.ClickByJS("#submit-federation", DefaultTimeout, token);
                    await Task.Delay(1000, token);

                    //retry click
                    try
                    {
                        driver.ExecuteScript("arguments[0].click();",
                            driver.FindElement(By.CssSelector("#submit-federation")));
                    }
                    catch { }

                    step = "WaitNextPage";
                    driver.FindElement("#idcs-signin-basic-signin-form-submit > button > div", DefaultTimeout * 2, token);
                }

                if (driver.Url.Contains("identity.oraclecloud.com/ui/v1/signin")) return Tuple.Create(true, string.Empty);
                else return Tuple.Create(false, $"can not load tenant page");
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[EnterTenant] {step}", ex);
                return Tuple.Create(false, $"[EnterTenant] {step} {ex.Message}");
            }
        }

        private static async Task HandleCookieAccept(UndetectedChromeDriver driver, CancellationToken token)
        {
            try
            {
                if (!FrmMain.ClickCookie) return;
                var iframe = driver.FindElement(@"iframe[name=""trustarc_cm""]", DefaultTimeout, token);
                driver.SwitchTo().Frame(iframe);
                await Task.Delay(3000, token);

                driver.Click("a.call", DefaultTimeout, token);
                await Task.Delay(3000, token);

                driver.SwitchTo().DefaultContent();
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog("[HandleCookieAccept]", ex);
            }
        }

        public static async Task<Tuple<bool, string, string>> Login(UndetectedChromeDriver driver, Account account, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "emailInputElm";
                var emailInputElm = driver.FindElement("#idcs-signin-basic-signin-form-username", DefaultTimeout, token);
                await Task.Delay(3000, token);
                driver.Sendkeys(emailInputElm, account.Email, true, DefaultTimeout, token);

                step = "passInputElm";
                var passInputElm = driver.FindElement(@"input[id=""idcs-signin-basic-signin-form-password|input""]", DefaultTimeout, token);
                await Task.Delay(3000, token);
                driver.Sendkeys(passInputElm, account.Password, true, DefaultTimeout, token);

                var tenantUrl = driver.Url.Split(".identity.oraclecloud.com")[0];
                step = "button";
                await Task.Delay(3000, token);
                driver.Click("#idcs-signin-basic-signin-form-submit > button > div", DefaultTimeout, token);

                step = "NextPage";
                await Task.Delay(DefaultTimeout * 1000, token);
                if (driver.Url.Contains("https://cloud.oracle.com/?region=")) return Tuple.Create(true, string.Empty, tenantUrl);
                if (driver.Url.Contains("identity.oraclecloud.com/ui/v1/pwdmustchange")) return Tuple.Create(true, "pwdmustchange", tenantUrl);
                
                if (Check2FA(driver, token)) return Tuple.Create(false, $"Login failed due to [2FA]", string.Empty);
                if (driver.Url.Contains("oraclecloud.com/ui/v1/signin")) return Tuple.Create(false, "login failed or can not load the next page", string.Empty);
                else return Tuple.Create(false, "login failed or can not load the next page", string.Empty);
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[Login] {step}", ex);
                return Tuple.Create(false, $"[Login] {step} {ex.Message}", string.Empty);
            }
        }

        private static bool Check2FA(UndetectedChromeDriver driver, CancellationToken token)
        {
            try
            {
                var _2faElm = driver.FindElement("#ui-id-1", 5, token);
                return _2faElm.Text.Contains("Enable Secure Verification");
            }
            catch { return false; }
        }

        public static async Task<Tuple<bool, string>> ChangePasswordMustChange(UndetectedChromeDriver driver, Account account, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "oldPassElm";
                var oldPassElm = driver.FindElement(@"[id=""idcs-unauthenticated-reset-password-old-password|input""]", DefaultTimeout, token);
                await Task.Delay(3000, token);
                driver.Sendkeys(oldPassElm, account.CurrentPassword, true, DefaultTimeout, token);

                step = "newPassElm";
                var newPassElm = driver.FindElement(@"[id=""idcs-unauthenticated-reset-password-new-password|input""]", DefaultTimeout, token);
                await Task.Delay(3000, token);
                driver.Sendkeys(newPassElm, account.Password + account.PasswordIndex, true, DefaultTimeout, token);

                step = "confirmPassElm";
                var confirmPassElm = driver.FindElement(@"[id=""idcs-unauthenticated-reset-password-confirm-password|input""]", DefaultTimeout, token);
                await Task.Delay(3000, token);
                driver.Sendkeys(confirmPassElm, account.Password + account.PasswordIndex, true, DefaultTimeout, token);

                step = "btnElm";
                await Task.Delay(3000, token);
                driver.Click(@"[id=""idcs-unauthenticated-reset-password-submit-button""]", DefaultTimeout, token);

                step = "waitForNextPage";
                await Task.Delay(DefaultTimeout * 1000, token);
                if (driver.Url.Contains("oracle.com/cloud/sign-in"))
                {
                    step = "accountNameElm";
                    var accountNameElm = driver.FindElement("#cloudAccountName", DefaultTimeout, token);
                    driver.Sendkeys(accountNameElm, account.Email.Split('@')[0], true, DefaultTimeout, token);
                    await Task.Delay(3000, token);

                    step = "button";
                    driver.Click("#cloudAccountButton", DefaultTimeout, token);
                    await Task.Delay(DefaultTimeout * 1000, token);
                }

                if (driver.Url.Contains("https://cloud.oracle.com/?region="))
                {
                    account.CurrentPassword = account.Password + account.PasswordIndex;
                    account.PasswordIndex++;
                    return Tuple.Create(true, driver.Url);
                }
                else return Tuple.Create(false, $"change password failed. Old pass: {account.Password}, new pass: {account.Password + account.PasswordIndex}");
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[ChangePassword] {step}", ex);
                return Tuple.Create(false, $"[ChangePassword] {step} {ex.Message}. Old pass: {account.Password}, new pass: {account.Password + account.PasswordIndex}");
            }
        }

        public static async Task<Tuple<bool, string>> ChangePassword(UndetectedChromeDriver driver, string tenantUrl, Account account, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "GoToURL";
                driver.GoToUrl($"{tenantUrl}.identity.oraclecloud.com/ui/v1/myconsole?root=my-info&my-info=change_password");
                await Task.Delay(3000, token);
                while (true)
                {
                    var oldPass = account.CurrentPassword;
                    var newPass = account.PasswordIndex == 5 ? account.NewPassword : account.Password + account.PasswordIndex;

                    step = "oldPassElm";
                    var oldPassElm = driver.FindElement("#oldpassword", DefaultTimeout, token);
                    await Task.Delay(3000, token);
                    driver.Sendkeys(oldPassElm, oldPass, true, DefaultTimeout, token);

                    step = "newPasswordElm";
                    var newPasswordElm = driver.FindElement("#mipwdpassword", DefaultTimeout, token);
                    await Task.Delay(3000, token);
                    driver.Sendkeys(newPasswordElm, newPass, true, DefaultTimeout, token);

                    step = "confirmPasswordElm";
                    var confirmPasswordElm = driver.FindElement("#cpassword", DefaultTimeout, token);
                    await Task.Delay(3000, token);
                    driver.Sendkeys(confirmPasswordElm, newPass, true, DefaultTimeout, token);

                    step = "btnElm";
                    await Task.Delay(3000, token);
                    driver.Click("#submitbutton", DefaultTimeout, token);

                    var timeOut = DateTime.Now.AddSeconds(DefaultTimeout);

                    step = "waitForSuccess";
                    var success = false;
                    while (DateTime.Now < timeOut)
                    {
                        if (string.IsNullOrEmpty(oldPassElm.GetAttribute("value")))
                        {
                            account.CurrentPassword = newPass;
                            account.PasswordIndex++;
                            success = true;
                            break;
                        }
                        await Task.Delay(2000, token);
                    }
                    if (!success) return Tuple.Create(false, $"change pass failed. Oldpass: {account.Password}, newPass {newPass}");
                    if (account.PasswordIndex == 6) return Tuple.Create(true, string.Empty);
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[ChangePassword] {step} {account.PasswordIndex}", ex);
                return Tuple.Create(false, $"[ChangePassword] {step} {account.PasswordIndex} {ex.Message}. Oldpass: {account.Password}, newPass {account.NewPassword}");
            }
            finally
            {
                await Task.Delay(DefaultWaitChangePass * 1000, token);
            }
        }

        #region delete or reboot or create instance
        public static async Task DeleteRebootAndCreate(UndetectedChromeDriver driver, Account account, string ssh, CancellationToken token)
        {
            var step = "GoToURL";
            try
            {
                driver.GoToUrl("https://cloud.oracle.com/compute/instances");
                await Task.Delay(DefaultTimeout * 1000 / 2, token);

                var tryTimes = 0;
                while (true)
                {
                    try
                    {
                        step = "iframe";
                        var iframe = driver.FindElement("#sandbox-compute-container", DefaultTimeout, token);
                        await Task.Delay(3000, token);
                        driver.SwitchTo().Frame(iframe);

                        var compartmentStatus = driver.FindElement(@"a[id*=""active_compartment_select""]", DefaultTimeout, token).GetAttribute("class");
                        if (compartmentStatus.Contains("bottom"))
                        {
                            step = "compartmentElm";
                            driver.ScrollTo(@"[aria-label=""Compartment""]", DefaultTimeout, token);
                            await Task.Delay(1000, token);
                            driver.ClickByJS(@"a[id*=""active_compartment_select""]", DefaultTimeout, token);
                        }

                        step = "rootElm";
                        await Task.Delay(3000, token);
                        driver.ClickByJS(@"label[title*=""(root)""] span", DefaultTimeout, token);

                        step = "waitTable";
                        _ = driver.FindElement(@"[role=""table""]", DefaultTimeout, token);
                        break;
                    }
                    catch
                    {
                        tryTimes++;
                        if (tryTimes == 2) throw;
                    }
                }

                await Task.Delay(DefaultTimeout * 1000 / 2, token);
                if (account.ToBeDeleteInstances.Any() || account.ToBeRebootInstances.Any())
                {
                    step = "instancesElm";
                    var instancesElm = driver.FindElements(By.CssSelector("table tbody tr"));

                    foreach (var instanceElm in instancesElm)
                    {
                        token.ThrowIfCancellationRequested();
                        await HandleDeleteOrReboot(driver, account, instanceElm, token);
                    }

                    if (account.ToBeDeleteInstances.Count > 0) await Task.Delay(DefaultWaitDelete * 1000, token);
                    else if (account.ToBeRebootInstances.Count > 0) await Task.Delay(DefaultWaitReboot * 1000, token);
                }

                if (!string.IsNullOrEmpty(account.ToBeCreateInstance))
                {
                    step = "createInstance";
                    await HandleCreateInstance(driver, account, ssh, token);
                    await Task.Delay(DefaultWaitCreateNew * 1000, token);
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[DeleteAndReboot] {step}", ex);
                account.Errors.Add($"[DeleteAndReboot] {step} {ex.Message}");
            }
        }

        private static async Task HandleCreateInstance(UndetectedChromeDriver driver, Account account, string ssh, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "createBtn";
                await Task.Delay(2000, token);
                driver.Click(".chakra-stack .chakra-button:nth-child(1)", DefaultTimeout, token);

                step = "displayNameElm";
                var displayNameElm = driver.FindElement(@"[data-test-id=""create-plugin-display-name-input""]", DefaultTimeout, token);
                await Task.Delay(2000, token);
                displayNameElm.ClearContentManually(DefaultTimeout, token);
                driver.Sendkeys(displayNameElm, account.ToBeCreateInstance, true, DefaultTimeout, token);

                step = "HandleShapeImage";
                await HandleShapeImage(driver, account, token);

                step = "HandleNetwork";
                await HandleNetwork(driver, token);

                step = "HandleSSHKey";
                await HandleSSHKey(driver, ssh, token);

                step = "HandleBootVolumeSize";
                await HandleBootVolumeSize(driver, account, token);

                step = "confirmCreateBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[aria-label=""Create compute instance""] .oui-savant__Panel--Footer button:nth-child(1)", DefaultTimeout, token);

                step = "waitDetailsPage";
                _ = driver.FindElement(@"[data-test-id=""start-button""]", DefaultTimeout, token);
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[HandleCreateInstance] {step}", ex);
                account.Errors.Add($"HandleCreateInstance: {step} {ex.Message}");
            }
        }

        private static async Task HandleBootVolumeSize(UndetectedChromeDriver driver, Account account, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "bootVolumeSizeBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[data-test-id=""_bootVolumeSize""]", DefaultTimeout, token);

                step = "bootVolumeElm";
                var bootVolumeElm = driver.FindElement(@".rc-slider.rc-slider-with-marks", DefaultTimeout, token);

                step = "bootVolumeHanlder";
                var bootVolumeHanlder = driver.FindElement(@".rc-slider.rc-slider-with-marks .rc-slider-handle", DefaultTimeout, token);
                var actions = new Actions(driver);

                step = "bootVolumeHanlderAction";
                actions.ClickAndHold(bootVolumeHanlder)
                    .MoveToElement(bootVolumeElm, bootVolumeElm.Size.Width, 0)
                    .Release()
                    .Perform();

                step = "useEncryptionElm";
                var useEncryptionElm = driver.FindElement(@"[name=""enableInTransitEncryption""]", DefaultTimeout, token);
                var encrypt = (bool)driver.ExecuteScript("return arguments[0].checked;", useEncryptionElm);
                if (encrypt)
                {
                    await Task.Delay(2000, token);
                    step = "useEncryptionElmClick";
                    driver.Click(@"[name=""enableInTransitEncryption""]", DefaultTimeout, token);
                }

                try
                {
                    step = "migrationsBtn";
                    var migrationsBtn = driver.FindElement(@"[name=""isLiveMigrationPreferred""]", DefaultTimeout, token);
                    await Task.Delay(2000, token);
                    driver.Click(@"[name=""isLiveMigrationPreferred""]", DefaultTimeout, token);

                    await Task.Delay(3000, token);
                    var isChecked = (bool)driver.ExecuteScript("return arguments[0].checked;", migrationsBtn);
                    if (isChecked)
                    {
                        step = "reClickMigrationsBtn";
                        driver.Click(@"[name=""isLiveMigrationPreferred""]", DefaultTimeout, token);
                    }
                }
                catch (Exception ex)
                {
                    DataHandler.WriteLog($"[HandleBootVolumeSize] {step}", ex);
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[HandleBootVolumeSize] {step}", ex);
                throw;
            }
        }

        private static async Task HandleShapeImage(UndetectedChromeDriver driver, Account account, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "shapeAndImgBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[data-test-id=""image-shape-fieldset""] button", DefaultTimeout, token);

                step = "changeImgBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[data-test-id=""change-image-button""]", DefaultTimeout, token);

                step = "osBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[value=""Canonical Ubuntu""]", DefaultTimeout, token);

                step = "ubuntu2004";
                await Task.Delay(2000, token);
                driver.Click(@"[aria-label=""Select row Canonical Ubuntu 20.04""]", DefaultTimeout, token);

                step = "saveImgBtn";
                await Task.Delay(3000, token);
                driver.Click(@"[role=""dialog""][aria-label=""Select an image""] .oui-savant__Panel--Footer button", DefaultTimeout, token);

                step = "changeShapeBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[data-test-id=""change-shape-button""]", DefaultTimeout, token);

                if (account.Image == ImgType.Ampere)
                {
                    step = "ampereBtn";
                    await Task.Delay(2000, token);
                    driver.ClickByJS(@"[data-test-id=""ampere""]", DefaultTimeout, token);

                    step = "selectShapeNameBtn";
                    await Task.Delay(2000, token);
                    driver.Click(@"[aria-label=""Select row VM.Standard.A1.Flex""]", DefaultTimeout, token);

                    var actions = new Actions(driver);

                    step = "cpuSlider";
                    var cpuSlider = driver.FindElement(".slider-VM_Standard_A1_Flexcores .rc-slider", DefaultTimeout, token);

                    step = "cpuSliderHandler";
                    var cpuSliderHandler = driver.FindElement(".slider-VM_Standard_A1_Flexcores .rc-slider-handle-2", DefaultTimeout, token);

                    step = "cpuSliderHandlerAction";
                    actions.ClickAndHold(cpuSliderHandler)
                        .MoveToElement(cpuSlider, cpuSlider.Size.Width, 0)
                        .Release()
                        .Perform();

                    await Task.Delay(2000, token);

                    step = "memorySlider";
                    var memorySlider = driver.FindElement(".slider-VM_Standard_A1_Flexmemory .rc-slider", DefaultTimeout, token);

                    step = "memorySliderHandler";
                    var memorySliderHandler = driver.FindElement(".slider-VM_Standard_A1_Flexmemory .rc-slider-handle-2", DefaultTimeout, token);

                    step = "memorySliderHandlerAction";
                    actions.ClickAndHold(memorySliderHandler)
                        .MoveToElement(memorySlider, memorySlider.Size.Width, 0)
                        .Release()
                        .Perform();
                }
                else if (account.Image == ImgType.Other)
                {
                    step = "otherBtn";
                    await Task.Delay(2000, token);
                    driver.ClickByJS(@"[data-test-id=""other""]", DefaultTimeout, token);
                }

                step = "selectShapeBtn";
                await Task.Delay(2000, token);
                driver.Click(@"[aria-label=""Browse all shapes""] .oui-savant__Panel--Footer button", DefaultTimeout, token);
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[HandleShapeImage] {step}", ex);
                throw;
            }
        }

        private static async Task HandleNetwork(UndetectedChromeDriver driver, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "networkEditBtn";
                await Task.Delay(2000, token);
                driver.ClickByJS(@"[data-test-id=""network-fieldset""] button", DefaultTimeout, token);

                bool needCreate;
                try
                {
                    step = "checkNeedCreate";
                    _ = driver.FindElement(@"[name=""subnetId""] optgroup[label=""Public subnets""] option", 10, token);
                    needCreate = false;
                }
                catch { needCreate = true; }

                if (needCreate)
                {
                    step = "newNetworkElm";
                    await Task.Delay(2000, token);
                    driver.Click(@"[data-test-id=""new-vcn""]", DefaultTimeout, token);

                    step = "newVCloudNetworkElm";
                    var newVCloudNetworkElm = driver.FindElement(@"[name=""vcnName""]", DefaultTimeout, token);
                    await Task.Delay(2000, token);
                    newVCloudNetworkElm.ClearContentManually(DefaultTimeout, token);
                    driver.Sendkeys(newVCloudNetworkElm, "Network-1", false, DefaultTimeout, token);

                    step = "newSubnetNameElm";
                    var newSubnetNameElm = driver.FindElement(@"[name=""subnetName""]", DefaultTimeout, token);
                    await Task.Delay(2000, token);
                    newSubnetNameElm.ClearContentManually(DefaultTimeout, token);
                    driver.Sendkeys(newSubnetNameElm, "Network-1", false, DefaultTimeout, token);
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[HandleNetwork] {step}", ex);
                throw;
            }
        }

        private static async Task HandleSSHKey(UndetectedChromeDriver driver, string key, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "sshElm";
                await Task.Delay(2000, token);
                driver.ClickByJS(@"[id=""oui-radio-ssh-key-from-textarea""]", DefaultTimeout, token);

                if (FrmMain.Paste)
                {
                    Paste(driver, @"[name=""sshKey""]", key, true, DefaultTimeout, token);
                }
                else
                {
                    driver.Sendkeys(@"[name=""sshKey""]", key, true, DefaultTimeout, token);
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[HandleSSHKey] {step}", ex);
                throw;
            }
        }

        public static void Paste(UndetectedChromeDriver driver, string selector, string content, bool needCompare, int timeout, CancellationToken token)
        {
            var waiter = ChromeDriverExtension.GetWaiter(driver, timeout);
            waiter.Until(webdriver =>
            {
                var element = driver.FindElement(By.CssSelector(selector));
                try
                {
                    element.Click();
                    Thread.Sleep(500);
                    element.Clear();
                    Thread.Sleep(500);
                }
                catch { }
                lock (_lockClipboard)
                {
                    FrmMain.Instance.SetClipboard(content);
                    element.SendKeys(OpenQA.Selenium.Keys.Control + "v");
                    Thread.Sleep(500);
                }
                if (!needCompare) return true;
                return ChromeDriverExtension.CompareContent(driver, element, content);
            }, token);
        }

        private static async Task HandleDeleteOrReboot(UndetectedChromeDriver driver, Account account, IWebElement element, CancellationToken token)
        {
            var step = string.Empty;
            try
            {
                step = "GetInnerText";
                var instanceInfo = driver.GetInnerText(element);
                var deleted = false;

                foreach (var tobeDelete in account.ToBeDeleteInstances)
                {
                    if (instanceInfo.Contains(tobeDelete))
                    {
                        step = "actionBtn";
                        var actionBtn = element.FindElement(By.CssSelector("td:nth-child(11) button"));
                        await Task.Delay(3000, token);
                        actionBtn.Click(driver, DefaultTimeout, token);

                        step = "deleteBtn";
                        await Task.Delay(3000, token);
                        driver.Click(@"body menu[style*=""visible""] div:nth-child(7) button div button", DefaultTimeout, token);

                        step = "deleteVolumeBtn";
                        await Task.Delay(3000, token);
                        driver.Click(@"[name=""delete-volume-field""]", DefaultTimeout, token);

                        step = "deletedConfirmBtn";
                        await Task.Delay(3000, token);
                        driver.Click(@"[type=""submit""]", DefaultTimeout, token);

                        deleted = true;
                        account.DeletedInstances.Add(tobeDelete);
                        break;
                    }
                }

                if (deleted) return;

                foreach (var tobeReboot in account.ToBeRebootInstances)
                {
                    if (instanceInfo.Contains(tobeReboot))
                    {
                        step = "actionBtn";
                        var actionBtn = element.FindElement(By.CssSelector("td:nth-child(11) button"));
                        await Task.Delay(3000, token);
                        actionBtn.Click(driver, DefaultTimeout, token);

                        step = "rebootBtn";
                        await Task.Delay(3000, token);
                        driver.Click(@"body menu[style*=""visible""] div:nth-child(5) button div button", DefaultTimeout, token);

                        step = "forceRebootBtn";
                        await Task.Delay(3000, token);
                        driver.Click(@"[name=""force-reboot-instance-field""]", DefaultTimeout, token);

                        step = "rebootConfirmBtn";
                        await Task.Delay(3000, token);
                        driver.Click(@"[type=""submit""]", DefaultTimeout, token);
                        account.RebootedInstances.Add(tobeReboot);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[HandleDeleteOrReboot] {step}", ex);
                account.Errors.Add($"HandleDeleteOrReboot: {step} {ex.Message}");
            }
        }
        #endregion

        public static async Task CaptureInstance(UndetectedChromeDriver driver, Account account, CancellationToken token)
        {
            var step = "GoToURL";
            try
            {
                driver.GoToUrl("https://cloud.oracle.com/compute/instances");
                await Task.Delay(DefaultTimeout * 1000 / 2, token);

                step = "iframe";
                var iframe = driver.FindElement("#sandbox-compute-container", DefaultTimeout, token);
                await Task.Delay(3000, token);
                driver.SwitchTo().Frame(iframe);

                step = "waitTable";
                _ = driver.FindElement(@"[role=""table""]", DefaultTimeout, token);
                await Task.Delay(3000, token);

                step = "instancesElm";
                var instancesElm = driver.FindElements(By.CssSelector("table tbody tr"));
                foreach (var instanceElm in instancesElm)
                {
                    var content = driver.GetInnerText(instanceElm);
                    var details = content.Replace("\r\n\t\r\n", "|").Replace("\r\n\t", "").Replace("\n", " ");
                    account.CurrentInstances.Add(details);
                }
            }
            catch (Exception ex)
            {
                DataHandler.WriteLog($"[CaptureInstance] {step}", ex);
                account.Errors.Add($"[CaptureInstance] {step} {ex.Message}");
            }
        }
    }
}