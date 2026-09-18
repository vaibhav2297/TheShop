# Authentication

## 1. Problem Statement

Customers of The Shop need a way to create an account and return to it later — to track orders, save shipping details, and check out without re-entering everything. Passwords add friction (forgotten, reused, phished) and a poor first impression for a premium brand. New shoppers also need to confirm they meet the legal age to buy products sold on The Shop, before any account is created. Without authentication, there is no order history, no admin panel access, and no way to enforce age compliance at the account level.

**Solution (one line):** Customers sign up and sign in using their email and a six-digit one-time code, with age confirmed at sign-up.

## 2. Functional Requirements

1. **FR-1:** A new user can create an account by providing their first name, last name, email address, and date of birth.
2. **FR-2:** A returning user can sign in by providing only their email address.
3. **FR-3:** During both sign-up and sign-in, the user is sent a six-digit numeric code by email to verify they own the address.
4. **FR-4:** The user enters the six-digit code on the next screen to complete the sign-up or sign-in.
5. **FR-5:** A user must be at least 19 years old (based on date of birth) to complete sign-up.
6. **FR-6:** Each email address can be linked to at most one account.
7. **FR-7:** The user can request a new code from the same screen if the first one does not arrive, after a short waiting period.
8. **FR-8:** Once signed in, the user remains signed in across browser sessions until they explicitly sign out or their session expires.
9. **FR-9:** A signed-in user can sign out from anywhere in the app, which ends the session immediately.

## 3. Functional Behaviors

### Behavior 1: Sign up with a new email
- **User does:** Opens the sign-up page, enters first name, last name, email, and date of birth, then submits.
- **User sees:** A confirmation that a six-digit code has been sent to the email they entered, and a screen prompting them to enter that code.

### Behavior 2: Enter the code to complete sign-up
- **User does:** Receives the email, types the six-digit code into the code-entry screen, and submits.
- **User sees:** A brief welcome confirmation, then lands on their account home (signed in).

### Behavior 3: Sign in with an existing email
- **User does:** Opens the sign-in page, enters their email, and submits.
- **User sees:** A confirmation that a six-digit code has been sent, and a screen prompting them to enter the code.

### Behavior 4: Enter the code to complete sign-in
- **User does:** Receives the email, types the six-digit code, and submits.
- **User sees:** A brief sign-in confirmation, then lands back where they were trying to go (or account home if they came in cold).

### Behavior 5: Request a new code
- **User does:** On the code-entry screen, clicks "Resend code" after waiting at least 60 seconds.
- **User sees:** A confirmation that a new code was sent. Any previous code is no longer accepted.

### Behavior 6: Sign out
- **User does:** Clicks "Sign out" from the account menu.
- **User sees:** A confirmation that they have signed out and a return to the public (signed-out) view of the site.

## 4. Constraints

- Users must be at least 19 years old at the time of sign-up (Ontario legal age).
- Each email address may have only one account.
- One-time codes are exactly six numeric digits.
- A code is valid for 10 minutes from the time it is sent. After that, it must be requested again.
- A user must wait at least 60 seconds between requesting one code and requesting another for the same email.
- A given code can only be used once. Submitting it successfully invalidates it; requesting a new code invalidates any earlier code.
- A user has at most 5 attempts to enter the correct code for a given session. After 5 failed attempts, the code is invalidated and they must restart the flow from the email screen.
- All emails for codes are sent in English and French (matching the site's localized content).
- First name and last name are required at sign-up and cannot be left blank.
- Date of birth must be a valid past date.

## 5. Edge Cases & Error Handling

- **Edge case:** A new user enters an email that is already registered → **User experience:** They see "An account with this email already exists." with a link to the sign-in page; no code is sent.
- **Edge case:** A returning user enters an email that has no account → **User experience:** They see "No account found for this email." with a link to the sign-up page; no code is sent.
- **Edge case:** A new user's date of birth makes them younger than 19 → **User experience:** They see "You must be at least 19 years old to create an account." and cannot proceed; no account is created and no code is sent.
- **Edge case:** The user enters an incorrect six-digit code → **User experience:** They see "Incorrect code. Please try again." and remain on the code-entry screen with their remaining attempt count made clear after the first failure.
- **Edge case:** The user enters the correct code but more than 10 minutes have passed → **User experience:** They see "This code has expired. Please request a new one." with a button to resend.
- **Edge case:** The user fails the code 5 times → **User experience:** They see "Too many incorrect attempts. Please start again." and are returned to the email entry screen.
- **Edge case:** The user clicks "Resend code" before 60 seconds have passed → **User experience:** The button is disabled and shows the remaining seconds; no new code is sent.
- **Edge case:** The user closes the browser between requesting the code and entering it → **User experience:** They can return to the code-entry screen by reopening the sign-in/sign-up flow with the same email, and the still-valid code (within 10 minutes) will work.
- **Edge case:** The user enters a malformed email (missing "@", obvious typos in format) → **User experience:** The form shows an inline validation message and the submit button stays disabled until the email is well-formed; no code is sent.
- **Edge case:** The code email does not arrive → **User experience:** After 60 seconds the user can click "Resend code" to trigger a new email; the screen suggests checking the spam folder.
- **Edge case:** A signed-in user's session expires while browsing → **User experience:** On their next action that requires authentication, they are returned to the sign-in screen with a brief "Please sign in again to continue." message; their cart contents are preserved.

## 6. Acceptance Criteria

- [ ] **AC-1:** A new user can complete sign-up by entering first name, last name, email, date of birth, receiving a code by email, and entering it correctly. Verifies FR-1, FR-3, FR-4.
- [ ] **AC-2:** A returning user can sign in by entering only their email, receiving a code by email, and entering it correctly. Verifies FR-2, FR-3, FR-4.
- [ ] **AC-3:** Sign-up is blocked with a clear message for any date of birth that makes the user younger than 19. Verifies FR-5.
- [ ] **AC-4:** Attempting to sign up with an email that already has an account shows "An account with this email already exists." and does not send a code. Verifies FR-6.
- [ ] **AC-5:** Attempting to sign in with an email that does not have an account shows "No account found for this email." and does not send a code.
- [ ] **AC-6:** A code submitted more than 10 minutes after being sent is rejected with an "expired" message and the user is prompted to request a new one.
- [ ] **AC-7:** Five incorrect code submissions in a row invalidate the code and return the user to the email-entry screen with a clear message.
- [ ] **AC-8:** The "Resend code" action is disabled for the first 60 seconds after a code is sent, and the remaining seconds are visible to the user. Verifies FR-7.
- [ ] **AC-9:** Requesting a new code invalidates any previously sent code for the same email.
- [ ] **AC-10:** A signed-in user remains signed in after closing and reopening the browser, until they sign out or their session expires. Verifies FR-8.
- [ ] **AC-11:** Clicking "Sign out" ends the session and returns the user to the public view of the site. Verifies FR-9.
- [ ] **AC-12:** All authentication screens, validation messages, and code emails are available in both English and French.

---
**Status:** Draft
**Created:** 2026-05-20
