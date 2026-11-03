#!/usr/bin/env bash
set -Eeuo pipefail

jq -ec '
  select(type == "array" and length > 0)
  | select(all(.[]; type == "object" and
      ((keys - ["clientId", "redirectUri", "displayName", "clientSecret", "requirePkce", "postLogoutRedirectUri"]) | length == 0) and
      (.clientId | type == "string" and test("^[A-Za-z0-9][A-Za-z0-9_-]*$")) and
      (.redirectUri | type == "string" and startswith("https://")) and
      ((.displayName // "") | type == "string") and
      ((.clientSecret // "") | type == "string") and
      ((.postLogoutRedirectUri // "") | type == "string" and (. == "" or startswith("https://"))) and
      ((.requirePkce // false) | type == "boolean") and
      ([.clientId, .redirectUri, (.displayName // ""), (.clientSecret // ""), (.postLogoutRedirectUri // "")] | all(.[]; (contains("\n") or contains("\r")) | not))))
  | select((map(.clientId) | unique | length) == length)
  | select(all(.[] | select(.clientId == "opensourcelab");
      .redirectUri == "https://users.eldervibe.dev/signin-oidc" and
      ((.clientSecret // "") == "") and ((.requirePkce // false) == true)))
  | if any(.[]; .clientId == "opensourcelab") then . else
      . + [{clientId:"opensourcelab", displayName:"OpenSourceLab", redirectUri:"https://users.eldervibe.dev/signin-oidc", requirePkce:true}]
    end
'
