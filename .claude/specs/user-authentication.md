# User Authentication

## 1. Problem Statement

Customers visiting The Shop need a way to create personal accounts and securely return to them so they can save addresses, view past orders, and complete purchases without re-entering their details on every visit. Without accounts, every visit is anonymous — the cart can't follow customers across devices, order history is invisible, and there's no foundation for any signed-in experience (admin panel, saved addresses, returns). Authentication is a prerequisite for most other personalized and post-purchase functionality on the site.

**Solution (one line):** Let customers create an account with their email and password, sign back in on any device, and recover access if they forget their password.

## 2. Functional Requirements

1. **FR-1:** A new visitor can create an account using an email address and a password.
2. **FR-2:** A returning customer can sign in using their email address and password.
3. **FR-3:** A signed-in customer can sign out from any page on the site.
4. **FR-4:** A customer who has forgotten their password can request a reset link to be sent to their email.
5. **FR-5:** A customer who has received a reset link can choose a new password and immediately sign in with it.
6. **FR-6:** The site remembers a signed-in customer between visits on the same device until they explicitly sign out.
7. **FR-7:** Customers can see which account is currently signed in (their email is shown in the account menu).
8. **FR-8:** Authentication state controls access to account-only pages (order history, saved addresses, admin panel); unauthenticated visitors are redirected to the sign-in page and returned to their original destination after signing in.
9. **FR-9:** Passwords are never displayed back to the customer and never sent by email.
10. **FR-10:** All authentication-related text (labels, instructions, confirmations, errors) is available in English and French.

## 3. Functional Behaviors

### Behavior 1: Create an account
- **User does:** Visits the sign-up page, enters an email and a password meeting the stated requirements, confirms the password, accepts the Terms of Service and Privacy Policy, and submits.
- **User sees:** A confirmation that the account was created, is immediately signed in, and lands on the home page (or the page they originally tried to reach).

### Behavior 2: Sign in
- **User does:** Visits the sign-in page, enters their email and password, and submits.
- **User sees:** Lands on the home page (or the page they originally tried to reach) in a signed-in state, with their email shown in the account menu.

### Behavior 3: Sign out
- **User does:** Opens the account menu and selects "Sign out."
- **User sees:** Returns to the home page in a signed-out state; the account menu now offers "Sign in" and "Create account" instead.

### Behavior 4: Request a password reset
- **User does:** On the sign-in page, clicks "Forgot password?", enters their email, and submits.
- **User sees:** A neutral confirmation that, if an account exists for that email, a reset link has been sent.

### Behavior 5: Reset password from email link
- **User does:** Opens the email, clicks the reset link, enters and confirms a new password, and submits.
- **User sees:** A confirmation that the password has been changed and is automatically signed in.

## 4. Constraints

- Passwords must be at least 8 characters and contain at least one letter and one number.
- A password reset link expires 24 hours after it is sent.
- A password reset link can only be used once.
- An email address can only be used for one account at a time.
- Users must accept the Terms of Service and Privacy Policy when creating an account (checkbox required).
- All authentication screens and messages must be fully localized in English and French (Canadian market requirement).
- For privacy, the system never reveals whether an email address is registered — both the "forgot password" flow and the sign-in error message use generic wording.
- Customers stay signed in across visits on the same device until they explicitly sign out; sessions are not shared across devices.
- _(Assumption: First version covers sign-up, sign-in, sign-out, and password reset only — email verification, two-factor authentication, social sign-in, change-email, and account deletion are out of scope and will be covered in later specs.)_

## 5. Edge Cases & Error Handling

- **Edge case:** User tries to sign up with an email that is already registered → **User experience:** Inline message: "An account with this email already exists." with links to "Sign in" and "Reset your password."
- **Edge case:** User enters an invalid email format → **User experience:** Inline message under the field: "Please enter a valid email address."
- **Edge case:** User enters a password that doesn't meet the requirements → **User experience:** Inline message under the password field indicating which requirements aren't met (length, letter, number).
- **Edge case:** User mistypes the password confirmation during sign-up or reset → **User experience:** Inline message: "Passwords don't match."
- **Edge case:** User submits the sign-in form with incorrect credentials → **User experience:** Generic message: "The email or password is incorrect." (does not reveal which field is wrong).
- **Edge case:** User submits a form with empty required fields → **User experience:** Required fields are highlighted with "This field is required."
- **Edge case:** User clicks a password reset link that has expired → **User experience:** A page explains the link has expired and offers a button to request a new one.
- **Edge case:** User clicks a reset link that has already been used → **User experience:** A page explains the link is no longer valid and offers a button to request a new one.
- **Edge case:** User submits the sign-in form repeatedly with wrong credentials → **User experience:** After several failed attempts in a short period, further sign-in attempts are temporarily blocked with: "Too many attempts. Please try again in a few minutes or reset your password."
- **Edge case:** User loses internet connection while submitting any auth form → **User experience:** Retry message: "Couldn't reach The Shop right now. Please check your connection and try again." The form's values are preserved (except passwords).
- **Edge case:** Signed-out user tries to open an account-only page directly via URL → **User experience:** Redirected to the sign-in page with the message "Please sign in to continue." After signing in, taken to the originally requested page.

## 6. Acceptance Criteria

- [ ] **AC-1:** A new visitor can complete sign-up with a valid email and password, accept the terms, and land in a signed-in state (verifies FR-1).
- [ ] **AC-2:** A returning customer can sign in with correct credentials and is taken to their intended destination (verifies FR-2 and FR-8).
- [ ] **AC-3:** A signed-in customer can sign out from any page, and afterward sees the signed-out state consistently across the site (verifies FR-3).
- [ ] **AC-4:** Requesting a password reset always produces the same neutral confirmation message, regardless of whether the email is registered (verifies FR-4 and the privacy constraint).
- [ ] **AC-5:** A valid, unused, unexpired reset link allows the user to set a new password and is then automatically signed in with it (verifies FR-5).
- [ ] **AC-6:** A reset link no longer works after it has been used once or after 24 hours have passed (verifies the single-use and expiry constraints).
- [ ] **AC-7:** A signed-in customer remains signed in after closing and reopening the site on the same device, and is signed out only after explicitly selecting "Sign out" (verifies FR-6).
- [ ] **AC-8:** Attempting to visit an account-only page while signed out redirects to the sign-in page and, after a successful sign-in, returns the user to the originally requested page (verifies FR-8).
- [ ] **AC-9:** The sign-in form responds with a single generic error message regardless of which field is wrong (verifies FR-9 and the privacy constraint).
- [ ] **AC-10:** All sign-up, sign-in, sign-out, and password reset screens and messages render correctly in both English and French when the site language is switched (verifies FR-10).
- [ ] **AC-11:** Account creation cannot proceed unless the Terms of Service and Privacy Policy checkbox is checked (verifies the Constraints item).
- [ ] **AC-12:** After multiple failed sign-in attempts in a short period, further attempts from that session are temporarily blocked with a clear message (verifies the rate-limit edge case).
- [ ] **AC-13:** Account-only pages, the account menu (showing the signed-in email), and the "Sign in / Sign out" controls accurately reflect the current authentication state at all times (verifies FR-7 and FR-8).

---
**Status:** Draft
**Created:** 2026-05-14
