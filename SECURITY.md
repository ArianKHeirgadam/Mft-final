# Security policy

- Never commit personal access tokens, JWT signing keys, database passwords, or administrator passwords.
- A token pasted into a chat or source file must be revoked immediately and replaced with a new least-privilege credential stored in a credential manager.
- Bootstrap credentials are for first-user provisioning only; disable bootstrap after the first SuperAdmin has been created.
- Passwords must be stored as BCrypt hashes. Never store or log plaintext passwords.
- All role and department-scope checks must be enforced by the API. Frontend route guards are not a security boundary.
- Student endpoints must never return correct answers before permitted release. Students may submit only their own in-progress attempts and must not provide scores.
- Do not run automatic schema migrations against a production database without review and a backup.
