(() => {
    const assistant = document.querySelector("[data-voice-assistant]");
    const form = document.getElementById("civil-plaint-form");
    if (!assistant || !form) return;

    const answerInput = assistant.querySelector("[data-assistant-answer]");
    const questionOutput = assistant.querySelector("[data-assistant-question]");
    const optionsPanel = assistant.querySelector("[data-assistant-options]");
    const optionsLabel = assistant.querySelector("[data-assistant-options-label]");
    const optionList = assistant.querySelector("[data-assistant-option-list]");
    const statusOutput = assistant.querySelector("[data-assistant-status]");
    const assistantPanel = assistant.querySelector("#voice-assistant-panel");
    const assistantToggle = assistant.querySelector("[data-assistant-toggle]");
    const listenButton = assistant.querySelector('[data-assistant-action="listen"]');
    const answerButton = assistant.querySelector('[data-assistant-action="answer"]');
    const skipButton = assistant.querySelector('[data-assistant-action="skip"]');
    const storagePrefix = "civil-plaint-voice-v1";
    const responseWaitMilliseconds = 10_000;
    const retryDelayMilliseconds = 5_000;
    const speechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    const recognition = speechRecognition ? new speechRecognition() : null;
    const speechSynthesis = window.speechSynthesis;

    const copy = {
        en: {
            eyebrow: "GUIDED INTAKE",
            title: "Voice form assistant",
            expandAssistant: "Expand voice assistance",
            collapseAssistant: "Collapse voice assistance",
            availableOptions: "Available options (say the number or name)",
            chooseOption: "Select option {index}: {option}",
            description: "Answer questions by voice or type. Your answers go into the existing plaint form for you to review.",
            answerLabel: "Your answer",
            answerPlaceholder: "Speak or type your answer",
            useAnswer: "Review & apply",
            begin: "Begin assistance",
            resume: "Resume assistance",
            listen: "Listen",
            repeat: "Repeat question",
            next: "Next question",
            back: "Go back",
            change: "Change my answer",
            skip: "Skip this question",
            read: "Read my details",
            pause: "Pause assistance",
            complete: "Complete form",
            controlsLabel: "Voice assistance controls",
            hint: "Voice commands: “next question”, “repeat question”, “go back”, “change my answer”, “skip this question”, “read my details”, “stop assistance”, and “complete form”.",
            privacy: "Voice recognition depends on your browser and may use its speech service. You can type instead; avoid speaking confidential information unless you are comfortable with your browser’s privacy terms.",
            ready: "Press Begin assistance to start. You can answer by voice or type.",
            unsupported: "Speech recognition is not available in this browser. You can still use the assistant by typing answers.",
            listening: "Listening… I’ll wait until 10 seconds after your last words before saving the answer.",
            awaitingCountdown: "Listening for your answer — {seconds}s remaining.",
            pauseCountdown: "Answer detected. Accepting after your pause — {seconds}s remaining.",
            dictationWaiting: "Your words appear below as you speak. Review them and select Review & apply when ready, or keep speaking.",
            responseTimeout: "I didn’t hear a response for 10 seconds. Your question is still here; press Listen to try again or type your answer.",
            emptyResponseRetry: "I didn’t catch a response. I’ll repeat this question in 5 seconds (attempt {attempt} of 3).",
            emptyResponseContinue: "I still didn’t hear an answer after three repeats, so I’ll move to the next question. You can return to this one later.",
            listenForAnswer: "Tap Listen and answer, or type your response.",
            recognitionError: "Voice recognition could not start. Check microphone permission, or type your answer.",
            storageError: "The browser could not save assistant progress. Progress may reset after a form submission.",
            noQuestions: "The questions for this step are not available yet. Complete the visible form step first.",
            skipped: "Skipped. Moving to the next question.",
            saved: "Answer added to the form.",
            fieldVoice: "Speak to add / edit",
            fieldListening: "Listening… I’ll wait until 10 seconds after your last words before saving this field.",
            fieldAwaitingCountdown: "Listening for your answer — {seconds}s remaining.",
            fieldPauseCountdown: "Answer detected. Accepting after your pause — {seconds}s remaining.",
            fieldSaved: "Field updated. Check the value above.",
            fieldNoAnswer: "No answer heard. I’ll repeat this field question in 5 seconds (attempt {attempt} of 3).",
            fieldNoAnswerDone: "I couldn’t hear an answer after three attempts. This field was not changed; click its voice button to try again or edit it directly.",
            unchanged: "Kept the current answer.",
            selectNature: "I could not match that answer to a plaint nature. Choose an option or say one of the listed suit types.",
            dateError: "I could not understand that date. Please enter it in the date field using the date picker.",
            yesNo: "Please say yes or no so I can update the document checklist.",
            yesDocument: "Please choose the file and checklist item below, then select Upload and extract. I will ask you to review the extracted text after upload.",
            noDocument: "Marked as not currently available.",
            reviewDocument: "OCR text was extracted for {document}. Review it against the original. Correct any affected form fields, then say “confirm” to continue or “correct” to review it again.",
            uploadNotReadable: "No readable text was extracted for {document}. Check the original file and upload a clearer or text-readable copy if needed. Say “confirm” to continue after reviewing.",
            reviewAgain: "Please compare the OCR text with the original document and correct any affected form fields. Say “confirm” when you are ready to continue.",
            confirmed: "Review recorded for this session. The OCR may contain errors; verify the original document.",
            uploadStillNeeded: "I have not detected that upload yet. Choose the file, map it to the requested checklist item, and select Upload and extract.",
            detailsEmpty: "No form details have been entered yet.",
            detailsHeading: "Your current form details are:",
            detailsEnd: "That is the information currently entered. You can change any answer before completing the form.",
            completed: "Final review: {summary}. I have not submitted or generated anything. Review the highlighted form details and checklist before using Generate plaint.",
            missing: "{count} required plaint field(s) are empty",
            requiredReady: "all required plaint fields are filled",
            assistancePaused: "Assistance paused. Your current question is saved; press Resume assistance to continue.",
            questionUnavailable: "There are no more questions in the current step.",
            currentValue: "Current answer: {value}. Say “keep current” to leave it unchanged, or give a replacement.",
            commandHelp: "You can say next question, repeat question, go back, change my answer, skip this question, read my details, stop assistance, or complete form.",
            command: {
                next: ["next question", "next"],
                repeat: ["repeat question", "repeat"],
                back: ["go back", "previous question", "back"],
                change: ["change my answer", "change answer", "edit answer"],
                skip: ["skip this question", "skip"],
                read: ["read my details", "read details"],
                pause: ["stop assistance", "pause assistance", "stop"],
                complete: ["complete form", "start final review", "final review"],
                confirm: ["confirm", "confirmed", "yes", "correct"],
                correct: ["correct", "change it", "edit it"],
                keep: ["keep current", "same answer", "leave it"]
            }
        },
        hi: {
            eyebrow: "निर्देशित जानकारी",
            title: "आवाज़ से फ़ॉर्म भरने में सहायता",
            expandAssistant: "आवाज़ सहायता खोलें",
            collapseAssistant: "आवाज़ सहायता बंद करें",
            availableOptions: "उपलब्ध विकल्प (क्रमांक या नाम बोलें)",
            chooseOption: "विकल्प {index}: {option} चुनें",
            description: "आवाज़ से उत्तर दें या लिखें। आपके उत्तर मौजूदा वादपत्र फ़ॉर्म में भरे जाएँगे ताकि आप उनकी समीक्षा कर सकें।",
            answerLabel: "आपका उत्तर",
            answerPlaceholder: "अपना उत्तर बोलें या लिखें",
            useAnswer: "समीक्षा करके लागू करें",
            begin: "सहायता शुरू करें",
            resume: "सहायता फिर शुरू करें",
            listen: "सुनें",
            repeat: "प्रश्न दोहराएँ",
            next: "अगला प्रश्न",
            back: "पीछे जाएँ",
            change: "मेरा उत्तर बदलें",
            skip: "यह प्रश्न छोड़ें",
            read: "मेरी जानकारी पढ़ें",
            pause: "सहायता रोकें",
            complete: "फ़ॉर्म की समीक्षा करें",
            controlsLabel: "आवाज़ सहायता नियंत्रण",
            hint: "आवाज़ के आदेश: “अगला प्रश्न”, “प्रश्न दोहराएँ”, “पीछे जाएँ”, “मेरा उत्तर बदलें”, “यह प्रश्न छोड़ें”, “मेरी जानकारी पढ़ें”, “सहायता रोकें” और “फ़ॉर्म की समीक्षा करें”।",
            privacy: "आवाज़ पहचान आपके ब्राउज़र पर निर्भर करती है और उसकी वाणी सेवा का उपयोग कर सकती है। आप उत्तर लिख भी सकते हैं; ब्राउज़र की गोपनीयता शर्तों से सहमत हुए बिना गोपनीय जानकारी न बोलें।",
            ready: "शुरू करने के लिए सहायता शुरू करें दबाएँ। आप बोलकर या लिखकर उत्तर दे सकते हैं।",
            unsupported: "इस ब्राउज़र में आवाज़ पहचान उपलब्ध नहीं है। आप उत्तर लिखकर सहायता का उपयोग कर सकते हैं।",
            listening: "सुन रहे हैं… आपके आख़िरी शब्दों के 10 सेकंड बाद ही उत्तर सुरक्षित होगा।",
            awaitingCountdown: "आपका उत्तर सुन रहे हैं — {seconds} सेकंड शेष।",
            pauseCountdown: "उत्तर मिल गया। आपके विराम के बाद स्वीकार होगा — {seconds} सेकंड शेष।",
            dictationWaiting: "आपके बोलते ही शब्द नीचे दिखाई देंगे। समीक्षा करके लागू करें दबाएँ या बोलना जारी रखें।",
            responseTimeout: "10 सेकंड तक कोई उत्तर सुनाई नहीं दिया। प्रश्न खुला है; फिर से सुनने के लिए सुनें दबाएँ या उत्तर लिखें।",
            emptyResponseRetry: "उत्तर सुनाई नहीं दिया। मैं 5 सेकंड बाद यह प्रश्न फिर से पूछूँगा (3 में से {attempt}वाँ प्रयास)।",
            emptyResponseContinue: "तीन बार दोहराने के बाद भी उत्तर नहीं मिला, इसलिए अब अगला प्रश्न पूछूँगा। आप बाद में इस प्रश्न पर वापस आ सकते हैं।",
            listenForAnswer: "सुनें दबाकर उत्तर दें या अपना उत्तर लिखें।",
            recognitionError: "आवाज़ पहचान शुरू नहीं हो सकी। माइक्रोफ़ोन अनुमति जाँचें या उत्तर लिखें।",
            storageError: "ब्राउज़र सहायता की प्रगति सुरक्षित नहीं कर सका। फ़ॉर्म भेजने के बाद प्रगति रीसेट हो सकती है।",
            noQuestions: "इस चरण के प्रश्न अभी उपलब्ध नहीं हैं। पहले दिखाई दे रहे फ़ॉर्म चरण को पूरा करें।",
            skipped: "प्रश्न छोड़ा गया। अगले प्रश्न पर जा रहे हैं।",
            saved: "उत्तर फ़ॉर्म में जोड़ दिया गया है।",
            fieldVoice: "बोलकर जोड़ें / बदलें",
            fieldListening: "सुन रहे हैं… आपके आख़िरी शब्दों के 10 सेकंड बाद ही यह फ़ील्ड सुरक्षित होगा।",
            fieldAwaitingCountdown: "आपका उत्तर सुन रहे हैं — {seconds} सेकंड शेष।",
            fieldPauseCountdown: "उत्तर मिल गया। आपके विराम के बाद स्वीकार होगा — {seconds} सेकंड शेष।",
            fieldSaved: "फ़ील्ड अपडेट हुआ। ऊपर दिया गया मान जाँचें।",
            fieldNoAnswer: "उत्तर सुनाई नहीं दिया। मैं 5 सेकंड बाद इस फ़ील्ड का प्रश्न फिर से पूछूँगा (3 में से {attempt}वाँ प्रयास)।",
            fieldNoAnswerDone: "तीन प्रयासों के बाद भी उत्तर सुनाई नहीं दिया। फ़ील्ड नहीं बदला गया; फिर कोशिश करने या सीधे संपादित करने के लिए उसका आवाज़ बटन दबाएँ।",
            unchanged: "मौजूदा उत्तर रखा गया है।",
            selectNature: "इस उत्तर से वादपत्र का प्रकार नहीं मिला। कोई विकल्प चुनें या सूची में से वाद का प्रकार बोलें।",
            dateError: "तारीख समझ नहीं आई। कृपया तारीख चुनने वाले फ़ील्ड में तारीख दर्ज करें।",
            yesNo: "दस्तावेज़ सूची बदलने के लिए कृपया हाँ या नहीं कहें।",
            yesDocument: "कृपया नीचे फ़ाइल और दस्तावेज़ सूची चुनें, फिर अपलोड और निकालें दबाएँ। अपलोड के बाद निकाले गए पाठ की समीक्षा पूछूँगा।",
            noDocument: "अभी उपलब्ध नहीं के रूप में दर्ज किया गया।",
            reviewDocument: "{document} का OCR पाठ निकाला गया है। मूल दस्तावेज़ से इसकी तुलना करें। संबंधित फ़ॉर्म फ़ील्ड सही करें और आगे बढ़ने के लिए “पुष्टि करें” कहें, या फिर से देखने के लिए “सुधारें” कहें।",
            uploadNotReadable: "{document} से पढ़ने योग्य पाठ नहीं निकला। मूल फ़ाइल जाँचें और आवश्यकता हो तो साफ़ या पाठ-पठनीय प्रति अपलोड करें। समीक्षा के बाद आगे बढ़ने के लिए “पुष्टि करें” कहें।",
            reviewAgain: "OCR पाठ की मूल दस्तावेज़ से तुलना करें और संबंधित फ़ॉर्म फ़ील्ड सही करें। तैयार होने पर “पुष्टि करें” कहें।",
            confirmed: "इस सत्र के लिए समीक्षा दर्ज की गई। OCR में त्रुटियाँ हो सकती हैं; मूल दस्तावेज़ जाँचें।",
            uploadStillNeeded: "यह अपलोड अभी दिखाई नहीं दिया। फ़ाइल चुनें, उसे मांगे गए दस्तावेज़ से जोड़ें और अपलोड तथा निकालें दबाएँ।",
            detailsEmpty: "अभी फ़ॉर्म में कोई जानकारी दर्ज नहीं है।",
            detailsHeading: "आपके फ़ॉर्म में अभी यह जानकारी है:",
            detailsEnd: "अभी इतनी जानकारी दर्ज है। फ़ॉर्म पूरा करने से पहले आप कोई भी उत्तर बदल सकते हैं।",
            completed: "अंतिम समीक्षा: {summary}। मैंने फ़ॉर्म भेजा या दस्तावेज़ तैयार नहीं किया है। वादपत्र तैयार करने से पहले फ़ॉर्म और दस्तावेज़ सूची की समीक्षा करें।",
            missing: "{count} आवश्यक वादपत्र फ़ील्ड खाली हैं",
            requiredReady: "सभी आवश्यक वादपत्र फ़ील्ड भरे हुए हैं",
            assistancePaused: "सहायता रोक दी गई है। मौजूदा प्रश्न सुरक्षित है; जारी रखने के लिए सहायता फिर शुरू करें दबाएँ।",
            questionUnavailable: "इस चरण में और प्रश्न नहीं हैं।",
            currentValue: "मौजूदा उत्तर: {value}। इसे रखने के लिए “मौजूदा उत्तर रखें” कहें या नया उत्तर दें।",
            commandHelp: "आप अगला प्रश्न, प्रश्न दोहराएँ, पीछे जाएँ, मेरा उत्तर बदलें, यह प्रश्न छोड़ें, मेरी जानकारी पढ़ें, सहायता रोकें या फ़ॉर्म की समीक्षा करें कह सकते हैं।",
            command: {
                next: ["अगला प्रश्न", "अगला"],
                repeat: ["प्रश्न दोहराएँ", "दोहराएँ"],
                back: ["पीछे जाएँ", "पिछला प्रश्न", "पीछे"],
                change: ["मेरा उत्तर बदलें", "उत्तर बदलें", "उत्तर संपादित करें"],
                skip: ["यह प्रश्न छोड़ें", "छोड़ें"],
                read: ["मेरी जानकारी पढ़ें", "जानकारी पढ़ें"],
                pause: ["सहायता रोकें", "सहायता बंद करें", "रुकें"],
                complete: ["फ़ॉर्म की समीक्षा करें", "अंतिम समीक्षा"],
                confirm: ["पुष्टि करें", "पुष्ट", "हाँ", "ठीक है"],
                correct: ["सुधारें", "इसे बदलें", "इसे संपादित करें"],
                keep: ["मौजूदा उत्तर रखें", "वही उत्तर", "इसे रहने दें"]
            }
        }
    };

    const prompts = {
        en: {
            nature: "What is the nature of the plaint or suit?",
            jurisdiction: "Which state or jurisdiction is this dispute in?",
            caseFacts: "Please briefly describe the case facts.",
            court: "Which court has jurisdiction over this dispute?",
            title: "What should the title of the case be?",
            number: "Has a case number already been assigned?",
            causeDate: "When did the dispute or cause of action arise?",
            plaintiff: "What is the plaintiff’s full name?",
            defendant: "Who is the defendant?",
            plaintiffAddress: "What is the plaintiff’s complete address?",
            defendantAddress: "What is the defendant’s complete address?",
            property: "Please describe the disputed property.",
            survey: "What is the survey or plot number?",
            area: "What is the total property area?",
            village: "In which village is the property situated?",
            tehsil: "What is the tehsil name?",
            district: "Which district does the property belong to?",
            valuation: "Has the suit valuation or applicable court fee been determined?",
            disputeStart: "When did the dispute begin?",
            parties: "Who are the parties involved in the dispute?",
            interest: "How did the plaintiff acquire an interest in the property?",
            possession: "Who currently possesses the property?",
            previousPartition: "Has any previous partition taken place?",
            denial: "Has the defendant denied the plaintiff’s rights?",
            previousNotice: "Are there any relevant notices or previous proceedings?",
            legalGrounds: "Has your advocate identified any legal grounds? State only grounds already identified by your advocate; do not guess.",
            ownershipDocuments: "Which ownership or inheritance documents are relevant to the dispute?",
            priorOrders: "Are there any relevant previous court orders or agreements?",
            partitionRelief: "Are you seeking partition of the property?",
            declarationRelief: "Are you seeking a declaration of ownership or rights?",
            injunctionRelief: "Are you requesting an injunction?",
            possessionRelief: "Are you seeking possession or any other relief?",
            prayer: "What specific order are you asking the court to make in the prayer?",
            place: "What place should appear in the plaint?",
            filingDate: "What date should appear on the plaint?",
            advocate: "What is the advocate’s name?",
            enrollment: "What is the advocate’s enrollment number?"
        },
        hi: {
            nature: "वादपत्र या मुकदमे का प्रकार क्या है?",
            jurisdiction: "यह विवाद किस राज्य या क्षेत्राधिकार में है?",
            caseFacts: "कृपया मामले के तथ्य संक्षेप में बताइए।",
            court: "इस विवाद पर किस न्यायालय का क्षेत्राधिकार है?",
            title: "मामले का शीर्षक क्या होना चाहिए?",
            number: "क्या मामले का नंबर पहले से दिया गया है?",
            causeDate: "विवाद या वाद-कारण कब उत्पन्न हुआ?",
            plaintiff: "वादी का पूरा नाम क्या है?",
            defendant: "प्रतिवादी कौन है?",
            plaintiffAddress: "वादी का पूरा पता क्या है?",
            defendantAddress: "प्रतिवादी का पूरा पता क्या है?",
            property: "कृपया विवादित संपत्ति का वर्णन करें।",
            survey: "सर्वेक्षण या भूखंड संख्या क्या है?",
            area: "संपत्ति का कुल क्षेत्रफल कितना है?",
            village: "संपत्ति किस गाँव में स्थित है?",
            tehsil: "तहसील का नाम क्या है?",
            district: "संपत्ति किस ज़िले में है?",
            valuation: "क्या वाद का मूल्यांकन या लागू न्यायालय शुल्क तय किया गया है?",
            disputeStart: "विवाद कब शुरू हुआ?",
            parties: "विवाद में कौन-कौन पक्ष शामिल हैं?",
            interest: "वादी को संपत्ति में अधिकार कैसे प्राप्त हुआ?",
            possession: "वर्तमान में संपत्ति पर किसका कब्ज़ा है?",
            previousPartition: "क्या पहले कभी बँटवारा हुआ है?",
            denial: "क्या प्रतिवादी ने वादी के अधिकारों से इनकार किया है?",
            previousNotice: "क्या कोई संबंधित नोटिस या पिछली कार्यवाही हुई है?",
            legalGrounds: "क्या आपके अधिवक्ता ने कोई कानूनी आधार पहचाना है? केवल अधिवक्ता द्वारा बताए गए आधार कहें; अनुमान न लगाएँ।",
            ownershipDocuments: "विवाद से संबंधित स्वामित्व या विरासत के कौन-से दस्तावेज़ हैं?",
            priorOrders: "क्या कोई संबंधित पिछला न्यायालय आदेश या समझौता है?",
            partitionRelief: "क्या आप संपत्ति का बँटवारा चाहते हैं?",
            declarationRelief: "क्या आप स्वामित्व या अधिकारों की घोषणा चाहते हैं?",
            injunctionRelief: "क्या आप निषेधाज्ञा चाहते हैं?",
            possessionRelief: "क्या आप कब्ज़ा या कोई अन्य राहत चाहते हैं?",
            prayer: "प्रार्थना में आप न्यायालय से कौन-सा विशिष्ट आदेश चाहते हैं?",
            place: "वादपत्र में कौन-सा स्थान लिखा जाए?",
            filingDate: "वादपत्र में कौन-सी तारीख लिखी जाए?",
            advocate: "अधिवक्ता का नाम क्या है?",
            enrollment: "अधिवक्ता की नामांकन संख्या क्या है?"
        }
    };

    const textLabels = {
        en: {
            disputeStart: "Dispute began",
            parties: "Parties involved",
            interest: "How plaintiff acquired an interest",
            possession: "Current possession",
            previousPartition: "Previous partition",
            denial: "Denial of plaintiff’s rights",
            previousNotice: "Notices or previous proceedings",
            legalGrounds: "Grounds identified by advocate",
            ownershipDocuments: "Ownership or inheritance documents",
            priorOrders: "Previous court orders or agreements",
            partitionRelief: "Partition sought",
            declarationRelief: "Declaration sought",
            injunctionRelief: "Injunction sought",
            possessionRelief: "Possession or other relief sought"
        },
        hi: {
            disputeStart: "विवाद शुरू होने का समय",
            parties: "विवाद के पक्षकार",
            interest: "वादी का संपत्ति में अधिकार",
            possession: "वर्तमान कब्ज़ा",
            previousPartition: "पिछला बँटवारा",
            denial: "वादी के अधिकारों से इनकार",
            previousNotice: "नोटिस या पिछली कार्यवाही",
            legalGrounds: "अधिवक्ता द्वारा बताए गए आधार",
            ownershipDocuments: "स्वामित्व या विरासत के दस्तावेज़",
            priorOrders: "पिछले न्यायालय आदेश या समझौते",
            partitionRelief: "बँटवारे की माँग",
            declarationRelief: "घोषणा की माँग",
            injunctionRelief: "निषेधाज्ञा की माँग",
            possessionRelief: "कब्ज़े या अन्य राहत की माँग"
        }
    };

    let language = document.documentElement.lang === "hi" ? "hi" : "en";
    let questions = [];
    let questionIndex = 0;
    let active = false;
    let recognitionActive = false;
    let listenWhenReady = false;
    let speechPending = false;
    let listeningDeadline = 0;
    let listeningTimeout = 0;
    let countdownInterval = 0;
    let recognitionRestartTimeout = 0;
    let retryTimeout = 0;
    let pendingTranscript = "";
    let pendingInterimTranscript = "";
    let recognitionFinalResults = new Set();
    let emptyResponseQuestionIndex = -1;
    let emptyResponseRetries = 0;
    let fieldVoiceTarget = null;
    let fieldVoiceButton = null;
    let fieldVoiceRetries = 0;
    let fieldVoiceWaitingForStop = false;
    let voiceDetected = false;
    let pendingCode = readStored("pending-code") || "";

    function readStored(key) {
        try {
            return window.sessionStorage.getItem(`${storagePrefix}-${key}`);
        } catch (error) {
            setStatus(copy[language].storageError);
            return null;
        }
    }

    function store(key, value) {
        try {
            window.sessionStorage.setItem(`${storagePrefix}-${key}`, value);
        } catch (error) {
            setStatus(copy[language].storageError);
        }
    }

    function setStatus(message) {
        if (statusOutput) statusOutput.textContent = message;
    }

    function setCommandCaption(action, caption) {
        const button = assistant.querySelector(`[data-assistant-action="${action}"]`);
        button.dataset.caption = caption;
        button.setAttribute("aria-label", caption);
        button.querySelector("[data-command-label]").textContent = caption;
    }

    function renderOptions(question) {
        optionList.replaceChildren();
        const element = question?.element;
        const options = element instanceof HTMLSelectElement
            ? Array.from(element.options).filter((option) => option.value && !option.disabled)
            : [];
        optionsPanel.hidden = options.length === 0;
        if (options.length === 0) return [];

        optionsLabel.textContent = copy[language].availableOptions;
        options.forEach((option, index) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "voice-assistant-option";
            button.dataset.assistantOption = option.value;
            const number = document.createElement("span");
            number.className = "voice-assistant-option-number";
            number.textContent = String(index + 1);
            const label = document.createElement("span");
            label.className = "voice-assistant-option-label";
            label.textContent = option.textContent.trim();
            button.append(number, label);
            button.setAttribute("aria-label", copy[language].chooseOption
                .replace("{index}", String(index + 1))
                .replace("{option}", option.textContent.trim()));
            button.setAttribute("aria-pressed", String(option.selected));
            optionList.append(button);
        });
        return options.map((option, index) => `${index + 1}: ${option.textContent.trim()}`);
    }

    function questionWithOptions(question, text) {
        const options = renderOptions(question);
        if (options.length === 0) return text;
        const list = options.join(language === "hi" ? ", " : ", ");
        return language === "hi"
            ? `${text} क्रमांक बोलकर चुनें। उपलब्ध विकल्प हैं: ${list}।`
            : `${text} Say an option number to choose. Available options are: ${list}.`;
    }

    function optionIndexFromAnswer(answer, optionCount) {
        const normalized = normalize(answer)
            .replace(/[०-९]/g, (digit) => String("०१२३४५६७८९".indexOf(digit)))
            .replace(/^(?:(?:option|number|choice)\s+|(?:विकल्प|नंबर|संख्या)\s*)+/, "")
            .trim();
        const digitMatch = normalized.match(/^(\d+)(?:st|nd|rd|th)?$/);
        if (digitMatch) {
            const index = Number(digitMatch[1]) - 1;
            return index >= 0 && index < optionCount ? index : -1;
        }

        const spokenNumbers = new Map([
            ["zero", 0], ["one", 1], ["first", 1], ["two", 2], ["second", 2],
            ["three", 3], ["third", 3], ["four", 4], ["fourth", 4],
            ["five", 5], ["fifth", 5], ["six", 6], ["sixth", 6],
            ["seven", 7], ["seventh", 7], ["eight", 8], ["eighth", 8],
            ["nine", 9], ["ninth", 9], ["ten", 10], ["tenth", 10],
            ["एक", 1], ["पहला", 1], ["पहली", 1], ["पहले", 1],
            ["दो", 2], ["दूसरा", 2], ["दूसरी", 2], ["दूसरे", 2],
            ["तीन", 3], ["तीसरा", 3], ["तीसरी", 3], ["तीसरे", 3],
            ["चार", 4], ["चौथा", 4], ["चौथी", 4], ["चौथे", 4],
            ["पाँच", 5], ["पांच", 5], ["पाँचवाँ", 5], ["पांचवां", 5],
            ["छह", 6], ["छः", 6], ["छठा", 6], ["छठी", 6],
            ["सात", 7], ["सातवाँ", 7], ["सातवां", 7],
            ["आठ", 8], ["आठवाँ", 8], ["आठवां", 8],
            ["नौ", 9], ["नौवाँ", 9], ["नौवां", 9],
            ["दस", 10], ["दसवाँ", 10], ["दसवां", 10],
            ["ek", 1], ["do", 2], ["teen", 3], ["char", 4], ["chaar", 4],
            ["paanch", 5], ["panch", 5], ["chhe", 6], ["cheh", 6],
            ["saat", 7], ["aath", 8], ["nau", 9], ["das", 10]
        ]);
        const number = spokenNumbers.get(normalized);
        if (number === undefined) return null;
        const index = number - 1;
        return index >= 0 && index < optionCount ? index : -1;
    }

    function findSelectOption(element, answer) {
        const options = Array.from(element.options).filter((option) => option.value && !option.disabled);
        const optionIndex = optionIndexFromAnswer(answer, options.length);
        if (optionIndex !== null) return optionIndex < 0 ? null : options[optionIndex];

        const normalizedAnswer = normalize(answer);
        return options.find((candidate) =>
            normalize(candidate.value) === normalizedAnswer ||
            normalize(candidate.textContent).includes(normalizedAnswer) ||
            normalizedAnswer.includes(normalize(candidate.textContent))) || null;
    }

    function setExpanded(expanded) {
        assistant.dataset.expanded = expanded ? "true" : "false";
        assistantPanel.hidden = !expanded;
        assistantToggle.setAttribute("aria-expanded", String(expanded));
        assistantToggle.setAttribute("aria-label", expanded
            ? copy[language].collapseAssistant
            : copy[language].expandAssistant);
        assistantToggle.textContent = expanded ? "−" : "+";
    }

    function setLanguage(nextLanguage) {
        language = nextLanguage === "hi" ? "hi" : "en";
        const words = copy[language];
        buildQuestions();
        assistant.querySelector("[data-assistant-eyebrow]").textContent = words.eyebrow;
        assistant.querySelector("[data-assistant-title]").textContent = words.title;
        setExpanded(assistant.dataset.expanded === "true");
        assistant.querySelector("[data-assistant-description]").textContent = words.description;
        assistant.querySelector("[data-assistant-answer-label]").textContent = words.answerLabel;
        answerInput.placeholder = words.answerPlaceholder;
        answerButton.textContent = words.useAnswer;
        optionsLabel.textContent = words.availableOptions;
        document.querySelectorAll("[data-field-voice-button]").forEach((button) => {
            button.textContent = words.fieldVoice;
            button.setAttribute("aria-label", `${words.fieldVoice}: ${button.dataset.fieldLabel}`);
        });
        setCommandCaption("start", active ? words.resume : words.begin);
        setCommandCaption("listen", words.listen);
        setCommandCaption("repeat", words.repeat);
        setCommandCaption("next", words.next);
        setCommandCaption("back", words.back);
        setCommandCaption("change", words.change);
        setCommandCaption("skip", words.skip);
        setCommandCaption("read", words.read);
        setCommandCaption("pause", words.pause);
        setCommandCaption("complete", words.complete);
        assistant.querySelector("[data-assistant-controls]").setAttribute("aria-label", words.controlsLabel);
        assistant.querySelector("[data-assistant-hint]").textContent = words.hint;
        assistant.querySelector("[data-assistant-privacy]").textContent = words.privacy;
        if (recognition) recognition.lang = language === "hi" ? "hi-IN" : "en-IN";
        renderQuestion();
    }

    function addField(id, promptKey, options = {}) {
        const element = document.getElementById(id);
        if (!element) return;
        questions.push({
            id,
            prompt: prompts,
            promptKey,
            element,
            optional: options.optional === true,
            appendLabel: options.appendLabel || "",
            documentCode: "",
            documentLabel: ""
        });
    }

    function buildQuestions() {
        questions = [];
        addField("Input_PlaintNature", "nature", { optional: true });
        addField("Input_Jurisdiction", "jurisdiction", { optional: true });
        addField("Input_Facts", "caseFacts", { optional: true });

        document.querySelectorAll("[data-voice-document-code]").forEach((checkbox) => {
            const label = checkbox.closest(".document-row")?.querySelector(".document-label strong")?.textContent?.trim();
            if (!label) return;
            questions.push({
                id: `document:${checkbox.dataset.voiceDocumentCode}`,
                prompt: null,
                promptKey: "",
                element: checkbox,
                optional: checkbox.dataset.voiceDocumentOptional === "true",
                appendLabel: "",
                documentCode: checkbox.dataset.voiceDocumentCode,
                documentLabel: label
            });
        });

        addField("Input_CourtName", "court");
        addField("Input_CaseTitle", "title");
        addField("Input_CaseNumber", "number", { optional: true });
        addField("Input_CauseOfActionDate", "causeDate", { optional: true });
        addField("Input_PlaintiffName", "plaintiff");
        addField("Input_DefendantName", "defendant");
        addField("Input_PlaintiffAddress", "plaintiffAddress");
        addField("Input_DefendantAddress", "defendantAddress");

        addField("Input_SuitPropertyDescription", "property", { optional: true });
        addField("Input_SurveyNumber", "survey", { optional: true });
        addField("Input_Area", "area", { optional: true });
        addField("Input_Village", "village", { optional: true });
        addField("Input_Tehsil", "tehsil", { optional: true });
        addField("Input_District", "district", { optional: true });
        addField("Input_Valuation", "valuation", { optional: true });

        [
            ["disputeStart", "disputeStart"],
            ["parties", "parties"],
            ["interest", "interest"],
            ["possession", "possession"],
            ["previousPartition", "previousPartition"],
            ["denial", "denial"],
            ["previousNotice", "previousNotice"]
        ].forEach(([key, promptKey]) => addField("Input_Facts", promptKey, {
            optional: true,
            appendLabel: textLabels[language][key]
        }));
        addField("Input_Grounds", "legalGrounds", {
            optional: true,
            appendLabel: textLabels[language].legalGrounds
        });
        [
            ["ownershipDocuments", "ownershipDocuments"],
            ["priorOrders", "priorOrders"]
        ].forEach(([key, promptKey]) => addField("Input_Facts", promptKey, {
            optional: true,
            appendLabel: textLabels[language][key]
        }));
        [
            ["partitionRelief", "partitionRelief"],
            ["declarationRelief", "declarationRelief"],
            ["injunctionRelief", "injunctionRelief"],
            ["possessionRelief", "possessionRelief"]
        ].forEach(([key, promptKey]) => addField("Input_ReliefsSought", promptKey, {
            optional: true,
            appendLabel: textLabels[language][key]
        }));
        addField("Input_Prayer", "prayer", { optional: true });

        addField("Input_Place", "place", { optional: true });
        addField("Input_Date", "filingDate", { optional: true });
        addField("Input_AdvocateName", "advocate", { optional: true });
        addField("Input_AdvocateEnrollmentNo", "enrollment", { optional: true });
        addFieldVoiceButtons();
    }

    function addFieldVoiceButtons() {
        form.querySelectorAll("input[id^='Input_'], textarea[id^='Input_'], select[id^='Input_']").forEach((element) => {
            if (["hidden", "checkbox", "file"].includes(element.type) ||
                element.disabled ||
                element.dataset.voiceFieldButtonAdded === "true") {
                return;
            }

            const label = form.querySelector(`label[for="${element.id}"]`)?.textContent?.trim();
            const field = element.closest(".field");
            if (!label || !field) return;

            const button = document.createElement("button");
            button.type = "button";
            button.className = "field-voice-button";
            button.dataset.fieldVoiceButton = "true";
            button.dataset.fieldId = element.id;
            button.dataset.fieldLabel = label.replace(/\s+/g, " ");
            button.textContent = copy[language].fieldVoice;
            button.setAttribute("aria-label", `${copy[language].fieldVoice}: ${button.dataset.fieldLabel}`);
            element.dataset.voiceFieldButtonAdded = "true";

            const actions = document.createElement("div");
            actions.className = "field-voice-actions";
            actions.append(button);
            const status = document.createElement("span");
            status.className = "field-voice-status";
            status.dataset.fieldVoiceStatus = element.id;
            status.setAttribute("role", "status");
            status.setAttribute("aria-live", "polite");
            actions.append(status);
            element.insertAdjacentElement("afterend", actions);
        });
    }

    function currentQuestion() {
        return questions[questionIndex] || null;
    }

    function questionText(question) {
        if (!question) return copy[language].questionUnavailable;
        if (question.documentCode) {
            return language === "hi"
                ? `क्या ${question.documentLabel} उपलब्ध है?`
                : `Do you have ${question.documentLabel} available?`;
        }
        return question.prompt[language][question.promptKey];
    }

    function findUploadedDocument(code) {
        return Array.from(document.querySelectorAll("[data-voice-uploaded-document]"))
            .find((item) => item.dataset.voiceUploadedDocument === code) || null;
    }

    function renderQuestion() {
        const question = currentQuestion();
        if (!question) {
            optionsPanel.hidden = true;
            optionList.replaceChildren();
            questionOutput.textContent = copy[language].questionUnavailable;
            skipButton.disabled = true;
            return questionOutput.textContent;
        }

        let text = questionText(question);
        if (question.documentCode && pendingCode === question.documentCode) {
            const uploaded = findUploadedDocument(question.documentCode);
            if (uploaded) {
                const hasText = Boolean(uploaded.querySelector(".voice-assistant-ocr-review .result-json"));
                const localizedTemplate = hasText ? copy[language].reviewDocument : copy[language].uploadNotReadable;
                text = localizedTemplate.replace("{document}", question.documentLabel);
                const details = uploaded.querySelector(".voice-assistant-ocr-review");
                if (details) details.open = true;
            } else {
                text = copy[language].yesDocument;
            }
        }

        const value = question.element.type === "checkbox"
            ? ""
            : question.element.value?.trim();
        if (value && !question.documentCode && questionIndex < 3) {
            text = `${text} ${copy[language].currentValue.replace("{value}", value.slice(0, 180))}`;
        }
        questionOutput.textContent = text;
        const speechText = questionWithOptions(question, text);
        skipButton.disabled = !question.optional;
        return speechText;
    }

    function say(text, listenAfter = false) {
        listenWhenReady = listenAfter && (active || fieldVoiceTarget) && Boolean(recognition);
        if (!speechSynthesis || !window.SpeechSynthesisUtterance) {
            speechPending = false;
            if (listenWhenReady && !fieldVoiceTarget) setStatus(copy[language].listenForAnswer);
            if (listenWhenReady) beginListeningWhenReady();
            return;
        }

        speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = language === "hi" ? "hi-IN" : "en-IN";
        utterance.onstart = () => {
            speechPending = true;
        };
        utterance.onend = () => {
            speechPending = false;
            if (listenWhenReady) beginListeningWhenReady();
        };
        utterance.onerror = () => {
            speechPending = false;
            if (listenWhenReady) beginListeningWhenReady();
        };
        speechSynthesis.speak(utterance);
    }

    function beginListeningWhenReady() {
        if (!listenWhenReady || !recognition) return;
        if (recognitionActive || speechPending) return;
        window.setTimeout(() => {
            if (!listenWhenReady || recognitionActive || speechPending) return;
            listenWhenReady = false;
            startListening();
        }, 300);
    }

    function clearListeningWindow() {
        window.clearTimeout(listeningTimeout);
        window.clearInterval(countdownInterval);
        window.clearTimeout(recognitionRestartTimeout);
        window.clearTimeout(retryTimeout);
        listeningTimeout = 0;
        countdownInterval = 0;
        recognitionRestartTimeout = 0;
        retryTimeout = 0;
        listeningDeadline = 0;
        voiceDetected = false;
        listenWhenReady = false;
    }

    function scheduleRetry(callback) {
        window.clearTimeout(retryTimeout);
        retryTimeout = window.setTimeout(() => {
            retryTimeout = 0;
            callback();
        }, retryDelayMilliseconds);
    }

    function startListening(continueWaiting = false) {
        if (!recognition) {
            setStatus(copy[language].unsupported);
            return;
        }
        if (recognitionActive) return;
        if (!continueWaiting || listeningDeadline <= Date.now()) {
            clearListeningWindow();
            pendingTranscript = "";
            pendingInterimTranscript = "";
            recognitionFinalResults.clear();
            listeningDeadline = Date.now() + responseWaitMilliseconds;
            resetSilenceTimer();
        }
        recognition.lang = language === "hi" ? "hi-IN" : "en-IN";
        recognition.continuous = true;
        recognition.interimResults = true;
        try {
            recognition.start();
        } catch (error) {
            clearListeningWindow();
            setVoiceStatus(copy[language].recognitionError);
        }
    }

    function resetSilenceTimer() {
        window.clearTimeout(listeningTimeout);
        listeningDeadline = Date.now() + responseWaitMilliseconds;
        listeningTimeout = window.setTimeout(finishListeningAfterSilence, responseWaitMilliseconds);
        updateVoiceCountdown();
        if (!countdownInterval) {
            countdownInterval = window.setInterval(updateVoiceCountdown, 250);
        }
    }

    function updateVoiceCountdown() {
        if (!listeningDeadline) return;
        const remainingSeconds = Math.max(0, Math.ceil((listeningDeadline - Date.now()) / 1000));
        const message = fieldVoiceTarget
            ? voiceDetected
                ? copy[language].fieldPauseCountdown
                : copy[language].fieldAwaitingCountdown
            : voiceDetected
                ? copy[language].pauseCountdown
                : copy[language].awaitingCountdown;
        setVoiceStatus(message.replace("{seconds}", String(remainingSeconds)));
    }

    function appendTranscript(text) {
        pendingTranscript = mergeTranscript(pendingTranscript, text);
    }

    function mergeTranscript(existingText, newText) {
        const existing = existingText.trim();
        const incoming = newText.trim();
        if (!existing) return incoming;
        if (!incoming) return existing;

        const existingWords = existing.split(/\s+/);
        const incomingWords = incoming.split(/\s+/);
        const normalizeWord = (word) => word
            .normalize("NFKC")
            .toLocaleLowerCase()
            .replace(/[^\p{L}\p{N}]/gu, "");
        const existingNormalized = existingWords.map(normalizeWord);
        const incomingNormalized = incomingWords.map(normalizeWord);

        const incomingIsExistingPrefix = incomingWords.length <= existingWords.length &&
            incomingNormalized.every((word, index) => word === existingNormalized[index]);
        if (incomingIsExistingPrefix) return existing;

        const existingIsIncomingPrefix = existingWords.length <= incomingWords.length &&
            existingNormalized.every((word, index) => word === incomingNormalized[index]);
        if (existingIsIncomingPrefix) return incoming;

        const sameSpokenStart = existingWords.length <= 8 &&
            incomingWords.length >= existingWords.length &&
            existingNormalized[0] &&
            existingNormalized[0] === incomingNormalized[0];
        if (sameSpokenStart) return incoming;

        const maxOverlap = Math.min(existingWords.length, incomingWords.length);
        for (let overlap = maxOverlap; overlap > 0; overlap -= 1) {
            const existingStart = existingWords.length - overlap;
            const overlaps = incomingNormalized.slice(0, overlap).every(
                (word, index) => word === existingNormalized[existingStart + index]);
            if (overlaps) {
                const remainder = incomingWords.slice(overlap).join(" ");
                return remainder ? `${existing} ${remainder}` : existing;
            }
        }

        return `${existing} ${incoming}`;
    }

    function getPendingTranscript() {
        return mergeTranscript(pendingTranscript, pendingInterimTranscript);
    }

    function finishListeningAfterSilence() {
        listeningTimeout = 0;
        window.clearTimeout(recognitionRestartTimeout);
        recognitionRestartTimeout = 0;
        const transcript = getPendingTranscript();
        pendingTranscript = "";
        pendingInterimTranscript = "";
        clearListeningWindow();
        if (recognitionActive) recognition.stop();

        if (!transcript) {
            handleEmptyResponse();
            return;
        }

        emptyResponseRetries = 0;
        answerInput.value = transcript;
        if (fieldVoiceTarget) {
            applyFieldAnswer(fieldVoiceTarget, transcript, { fieldVoice: true });
            return;
        }
        processAnswer(transcript);
    }

    function submitTypedAnswer() {
        const answer = answerInput.value;
        clearListeningWindow();
        pendingTranscript = "";
        pendingInterimTranscript = "";
        if (recognitionActive) recognition.stop();
        if (fieldVoiceTarget) {
            applyFieldAnswer(fieldVoiceTarget, answer, { fieldVoice: true });
        } else {
            processAnswer(answer);
        }
        answerInput.value = "";
    }

    function setVoiceStatus(message) {
        setStatus(message);
        if (fieldVoiceButton) {
            const fieldStatus = form.querySelector(`[data-field-voice-status="${fieldVoiceButton.dataset.fieldId}"]`);
            if (fieldStatus) fieldStatus.textContent = message;
        }
    }

    function fieldQuestionFor(element) {
        return questions.find((question) =>
            question.element === element &&
            !question.documentCode &&
            !question.appendLabel) || null;
    }

    function fieldPrompt(question) {
        const label = fieldVoiceButton?.dataset.fieldLabel || "";
        const existingValue = question.element.value?.trim();
        const prompt = question.prompt[language][question.promptKey];
        if (existingValue) {
            return language === "hi"
                ? `${label} में अभी यह दर्ज है: ${existingValue.slice(0, 180)}। नया उत्तर बोलकर इसे बदलें। ${prompt}`
                : `${label} currently says: ${existingValue.slice(0, 180)}. Speak a new answer to replace it. ${prompt}`;
        }
        return prompt;
    }

    function startFieldPrompt() {
        if (!fieldVoiceTarget) return;
        const prompt = fieldPrompt(fieldVoiceTarget);
        questionOutput.textContent = prompt;
        const speechPrompt = questionWithOptions(fieldVoiceTarget, prompt);
        setVoiceStatus(copy[language].fieldListening);
        say(speechPrompt, true);
    }

    function startFieldVoice(button) {
        setExpanded(true);
        if (!recognition) {
            fieldVoiceButton = button;
            setVoiceStatus(copy[language].unsupported);
            fieldVoiceButton = null;
            return;
        }

        const element = document.getElementById(button.dataset.fieldId);
        const question = element && fieldQuestionFor(element);
        if (!question) return;

        active = false;
        setCommandCaption("start", copy[language].begin);
        storeProgress();
        if (speechSynthesis) speechSynthesis.cancel();
        clearListeningWindow();
        fieldVoiceTarget = question;
        fieldVoiceButton = button;
        fieldVoiceRetries = 0;
        pendingTranscript = "";
        pendingInterimTranscript = "";
        fieldVoiceWaitingForStop = recognitionActive;
        if (recognitionActive) {
            recognition.stop();
            return;
        }
        startFieldPrompt();
    }

    function storeProgress() {
        store("active", active ? "true" : "false");
        store("index", String(questionIndex));
        store("pending-code", pendingCode);
    }

    function askCurrent(speak = true, listenAfter = false) {
        const question = currentQuestion();
        if (question?.documentCode &&
            !pendingCode &&
            findUploadedDocument(question.documentCode)) {
            pendingCode = question.documentCode;
        }
        const speechText = renderQuestion();
        if (!question) {
            setStatus(copy[language].questionUnavailable);
            return;
        }
        storeProgress();
        if (question.documentCode && pendingCode === question.documentCode && !findUploadedDocument(question.documentCode)) {
            setStatus(copy[language].uploadStillNeeded);
            if (speak) say(copy[language].yesDocument, listenAfter);
            return;
        }
        setStatus(listenAfter ? copy[language].listenForAnswer : "");
        if (speak) say(speechText, listenAfter);
    }

    function resumeQuestion() {
        active = true;
        storeProgress();
        if (questions.length === 0) {
            setStatus(copy[language].noQuestions);
            return;
        }
        questionIndex = Math.max(0, Math.min(questionIndex, questions.length - 1));
        askCurrent(true, true);
    }

    function submitResearch() {
        active = true;
        questionIndex = Math.min(3, questions.length);
        pendingCode = "";
        storeProgress();
        const researchButton = form.querySelector('button[formaction*="handler=Research"]');
        if (!researchButton) {
            setStatus(copy[language].noQuestions);
            return;
        }
        researchButton.click();
    }

    function advance(message = "") {
        const question = currentQuestion();
        if (question?.documentCode && pendingCode === question.documentCode) {
            pendingCode = "";
        }
        emptyResponseQuestionIndex = -1;
        emptyResponseRetries = 0;
        questionIndex += 1;
        storeProgress();
        if (questionIndex >= questions.length) {
            if (!document.querySelector("[data-voice-document-checklist]")) {
                submitResearch();
                return;
            }
            setStatus(copy[language].questionUnavailable);
            return;
        }
        if (message) setStatus(message);
        askCurrent(true, active);
    }

    function normalize(value) {
        return value.trim().toLocaleLowerCase(language === "hi" ? "hi-IN" : "en-IN")
            .replace(/[.,!?।]/g, "")
            .replace(/\s+/g, " ");
    }

    function matchedCommand(answer, commandName) {
        const value = normalize(answer);
        return copy[language].command[commandName].some((command) =>
            value === normalize(command) || value.startsWith(`${normalize(command)} `));
    }

    function handleCommand(answer) {
        if (matchedCommand(answer, "next")) {
            if (pendingCode && findUploadedDocument(pendingCode)) {
                acceptDocumentReview();
                return true;
            }
            advance();
            return true;
        }
        if (matchedCommand(answer, "repeat")) {
            askCurrent(true, active);
            return true;
        }
        if (matchedCommand(answer, "back")) {
            questionIndex = Math.max(0, questionIndex - 1);
            pendingCode = "";
            storeProgress();
            askCurrent(true, active);
            return true;
        }
        if (matchedCommand(answer, "change")) {
            answerInput.focus();
            setStatus(copy[language].listenForAnswer);
            if (recognition && recognitionActive) {
                listenWhenReady = true;
                recognition.stop();
            } else if (recognition) {
                startListening();
            }
            return true;
        }
        if (matchedCommand(answer, "skip")) {
            skipCurrent();
            return true;
        }
        if (matchedCommand(answer, "read")) {
            readDetails();
            return true;
        }
        if (matchedCommand(answer, "pause")) {
            pause();
            return true;
        }
        if (matchedCommand(answer, "complete")) {
            completeForm();
            return true;
        }
        return false;
    }

    function affirmativeOrNegative(answer) {
        const value = normalize(answer);
        const negative = language === "hi"
            ? ["नहीं", "नही", "उपलब्ध नहीं", "मेरे पास नहीं"]
            : ["no", "not available", "do not", "don't", "i don't", "not yet"];
        const positive = language === "hi"
            ? ["हाँ", "हां", "उपलब्ध है", "मेरे पास है"]
            : ["yes", "i have", "have it", "available", "i do", "i have it"];
        if (negative.some((word) => value === normalize(word) || value.includes(normalize(word)))) return false;
        if (positive.some((word) => value === normalize(word) || value.includes(normalize(word)))) return true;
        return null;
    }

    function handleDocumentAnswer(question, answer) {
        if (pendingCode === question.documentCode && findUploadedDocument(question.documentCode)) {
            if (matchedCommand(answer, "correct")) {
                setStatus(copy[language].reviewAgain);
                say(copy[language].reviewAgain, true);
                return;
            }
            if (matchedCommand(answer, "confirm") || matchedCommand(answer, "next")) {
                acceptDocumentReview();
                return;
            }
            setStatus(copy[language].reviewAgain);
            say(copy[language].reviewAgain, true);
            return;
        }

        const haveDocument = affirmativeOrNegative(answer);
        if (haveDocument === null) {
            setStatus(copy[language].yesNo);
            say(copy[language].yesNo, true);
            return;
        }

        question.element.checked = haveDocument;
        question.element.dispatchEvent(new Event("change", { bubbles: true }));
        if (!haveDocument) {
            pendingCode = "";
            advance(copy[language].noDocument);
            return;
        }

        pendingCode = question.documentCode;
        storeProgress();
        const mappedSelect = Array.from(form.querySelectorAll('select[name="documentCodes"]'))
            .find((select) => Array.from(select.options).some((option) => option.value === question.documentCode));
        if (mappedSelect) {
            mappedSelect.value = question.documentCode;
            mappedSelect.dispatchEvent(new Event("change", { bubbles: true }));
        }
        const fileInput = mappedSelect?.closest(".upload-row")?.querySelector('input[type="file"]');
        if (fileInput) fileInput.focus();
        setStatus(copy[language].yesDocument);
        say(copy[language].yesDocument);
    }

    function acceptDocumentReview() {
        const uploaded = findUploadedDocument(pendingCode);
        if (uploaded) {
            const review = uploaded.querySelector(".voice-assistant-ocr-review");
            if (review) review.open = false;
        }
        pendingCode = "";
        storeProgress();
        setStatus(copy[language].confirmed);
        advance(copy[language].confirmed);
    }

    function applyFieldAnswer(question, answer, options = {}) {
        const element = question.element;
        if (options.fieldVoice &&
            matchedCommand(answer, "keep") &&
            element.value.trim()) {
            finishFieldVoice(copy[language].fieldSaved);
            return;
        }
        if (!options.fieldVoice && matchedCommand(answer, "keep") && element.value.trim()) {
            advance(copy[language].unchanged);
            return;
        }

        let value = answer.trim();
        if (element instanceof HTMLSelectElement) {
            const option = findSelectOption(element, value);
            if (!option) {
                setVoiceStatus(copy[language].selectNature);
                say(copy[language].selectNature, Boolean(options.fieldVoice || active));
                return;
            }
            value = option.value;
        } else if (element.type === "date") {
            value = parseDate(value);
            if (!value) {
                setVoiceStatus(copy[language].dateError);
                say(copy[language].dateError, Boolean(options.fieldVoice || active));
                element.focus();
                return;
            }
        }

        if (question.appendLabel) {
            const entry = `${question.appendLabel}: ${value}`;
            if (!element.value.includes(entry)) {
                element.value = element.value.trim()
                    ? `${element.value.trim()}\n${entry}`
                    : entry;
            }
        } else {
            element.value = value;
        }
        element.dispatchEvent(new Event("input", { bubbles: true }));
        element.dispatchEvent(new Event("change", { bubbles: true }));
        if (element instanceof HTMLSelectElement) renderOptions(question);
        if (options.fieldVoice) {
            finishFieldVoice(copy[language].fieldSaved);
            return;
        }
        setStatus(copy[language].saved);
        advance(copy[language].saved);
    }

    function finishFieldVoice(message) {
        const completedElement = fieldVoiceTarget?.element;
        clearListeningWindow();
        if (recognition && recognitionActive) recognition.stop();
        fieldVoiceRetries = 0;
        fieldVoiceTarget = null;
        fieldVoiceWaitingForStop = false;
        setVoiceStatus(message);
        fieldVoiceButton = null;
        setExpanded(false);

        if (completedElement) {
            const fields = Array.from(form.querySelectorAll(
                'input[id^="Input_"], textarea[id^="Input_"], select[id^="Input_"]'));
            const currentIndex = fields.indexOf(completedElement);
            const nextField = fields.slice(currentIndex + 1).find((field) =>
                !field.disabled &&
                !field.readOnly &&
                !["hidden", "checkbox", "file", "radio", "submit", "button"].includes(field.type) &&
                field.getClientRects().length > 0);

            if (nextField) {
                nextField.focus({ preventScroll: true });
                nextField.scrollIntoView({ behavior: "smooth", block: "center" });
            } else {
                completedElement.focus({ preventScroll: true });
            }
        }
    }

    function parseDate(value) {
        const text = value.trim();
        if (text.toLocaleLowerCase() === "today" || text === "आज") {
            const today = new Date();
            return formatDateParts(today.getFullYear(), today.getMonth() + 1, today.getDate());
        }
        const isoDate = text.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (isoDate) {
            return formatDateParts(
                Number(isoDate[1]),
                Number(isoDate[2]),
                Number(isoDate[3]));
        }
        const indianDate = text.match(/^(\d{1,2})[/. -](\d{1,2})[/. -](\d{4})$/);
        if (indianDate) {
            return formatDateParts(
                Number(indianDate[3]),
                Number(indianDate[2]),
                Number(indianDate[1]));
        }
        const parsed = new Date(text);
        if (Number.isNaN(parsed.getTime())) return "";
        return formatDateParts(parsed.getFullYear(), parsed.getMonth() + 1, parsed.getDate());
    }

    function formatDateParts(year, month, day) {
        const date = new Date(year, month - 1, day);
        if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return "";
        return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
    }

    function processAnswer(answer) {
        const value = answer.trim();
        if (!value) {
            setStatus(copy[language].listenForAnswer);
            return;
        }
        emptyResponseQuestionIndex = -1;
        emptyResponseRetries = 0;
        if (handleCommand(value)) return;
        const question = currentQuestion();
        if (!question) {
            setStatus(copy[language].questionUnavailable);
            return;
        }
        if (question.documentCode) {
            handleDocumentAnswer(question, value);
            return;
        }
        applyFieldAnswer(question, value);
    }

    function handleEmptyResponse() {
        if (fieldVoiceTarget) {
            if (fieldVoiceRetries < 3) {
                fieldVoiceRetries += 1;
                const retryMessage = copy[language].fieldNoAnswer
                    .replace("{attempt}", String(fieldVoiceRetries));
                setVoiceStatus(retryMessage);
                const retryTarget = fieldVoiceTarget;
                scheduleRetry(() => {
                    if (fieldVoiceTarget !== retryTarget) return;
                    fieldVoiceWaitingForStop = recognitionActive;
                    if (recognitionActive) recognition.stop();
                    else startFieldPrompt();
                });
                return;
            }

            fieldVoiceTarget = null;
            fieldVoiceWaitingForStop = false;
            fieldVoiceRetries = 0;
            setVoiceStatus(copy[language].fieldNoAnswerDone);
            fieldVoiceButton = null;
            return;
        }

        if (emptyResponseQuestionIndex !== questionIndex) {
            emptyResponseQuestionIndex = questionIndex;
            emptyResponseRetries = 0;
        }

        if (emptyResponseRetries < 3) {
            emptyResponseRetries += 1;
            const retryMessage = copy[language].emptyResponseRetry
                .replace("{attempt}", String(emptyResponseRetries));
            setStatus(retryMessage);
            const retryQuestionIndex = questionIndex;
            const retryActiveState = active;
            scheduleRetry(() => {
                if (questionIndex !== retryQuestionIndex || active !== retryActiveState) return;
                askCurrent(true, active);
            });
            return;
        }

        advance(copy[language].emptyResponseContinue);
    }

    function skipCurrent() {
        const question = currentQuestion();
        if (!question) return;
        if (!question.optional) {
            setStatus(language === "hi"
                ? "यह आवश्यक प्रश्न है; इसे छोड़ा नहीं जा सकता।"
                : "This question is required and cannot be skipped.");
            return;
        }
        if (question.documentCode) {
            question.element.checked = false;
            question.element.dispatchEvent(new Event("change", { bubbles: true }));
        }
        pendingCode = "";
        advance(copy[language].skipped);
    }

    function pause() {
        active = false;
        clearListeningWindow();
        pendingTranscript = "";
        pendingInterimTranscript = "";
        fieldVoiceTarget = null;
        fieldVoiceButton = null;
        fieldVoiceWaitingForStop = false;
        if (recognition && recognitionActive) recognition.stop();
        if (speechSynthesis) speechSynthesis.cancel();
        assistant.dataset.listening = "false";
        storeProgress();
        setCommandCaption("start", copy[language].resume);
        setStatus(copy[language].assistancePaused);
    }

    function readDetails() {
        const details = Array.from(form.querySelectorAll("input[id^='Input_'], textarea[id^='Input_'], select[id^='Input_']"))
            .filter((element) => !["hidden", "checkbox", "file"].includes(element.type))
            .map((element) => {
                const label = form.querySelector(`label[for="${element.id}"]`)?.textContent?.trim();
                const value = element instanceof HTMLSelectElement
                    ? element.selectedOptions[0]?.textContent?.trim()
                    : element.value.trim();
                return label && value ? `${label}: ${value.slice(0, 240)}` : "";
            })
            .filter(Boolean);
        if (details.length === 0) {
            setStatus(copy[language].detailsEmpty);
            say(copy[language].detailsEmpty);
            return;
        }
        const summary = `${copy[language].detailsHeading} ${details.join(". ")}. ${copy[language].detailsEnd}`;
        setStatus(copy[language].detailsHeading);
        say(summary);
    }

    function completeForm() {
        const requiredIds = [
            "Input_CourtName",
            "Input_CaseTitle",
            "Input_PlaintiffName",
            "Input_PlaintiffAddress",
            "Input_DefendantName",
            "Input_DefendantAddress",
            "Input_Facts"
        ];
        const missing = requiredIds
            .map((id) => document.getElementById(id))
            .filter((element) => element && !element.value.trim());
        const missingDocuments = Array.from(document.querySelectorAll("[data-voice-document-code]"))
            .filter((checkbox) => checkbox.dataset.voiceDocumentOptional !== "true" &&
                !checkbox.checked &&
                !findUploadedDocument(checkbox.dataset.voiceDocumentCode));
        const missingCount = missing.length + missingDocuments.length;
        const summary = missingCount
            ? copy[language].missing.replace("{count}", String(missingCount))
            : copy[language].requiredReady;
        active = false;
        clearListeningWindow();
        if (recognition && recognitionActive) recognition.stop();
        if (speechSynthesis) speechSynthesis.cancel();
        storeProgress();
        const firstMissing = missing[0] || missingDocuments[0];
        if (firstMissing) {
            firstMissing.scrollIntoView({ behavior: "smooth", block: "center" });
            firstMissing.focus?.();
        } else {
            form.scrollIntoView({ behavior: "smooth", block: "start" });
        }
        const message = copy[language].completed.replace("{summary}", summary);
        questionOutput.textContent = message;
        setStatus(message);
        say(message);
    }

    function handleAction(action) {
        if (action === "start") {
            setExpanded(true);
            resumeQuestion();
        } else if (action === "listen") {
            listenWhenReady = false;
            startListening();
        } else if (action === "answer") {
            submitTypedAnswer();
        } else if (action === "repeat") {
            askCurrent(true, active);
        } else if (action === "next") {
            if (pendingCode && findUploadedDocument(pendingCode)) acceptDocumentReview();
            else advance();
        } else if (action === "back") {
            questionIndex = Math.max(0, questionIndex - 1);
            pendingCode = "";
            storeProgress();
            askCurrent(true, active);
        } else if (action === "change") {
            answerInput.focus();
            setStatus(copy[language].listenForAnswer);
            if (recognition) startListening();
        } else if (action === "skip") {
            skipCurrent();
        } else if (action === "read") {
            readDetails();
        } else if (action === "pause") {
            pause();
        } else if (action === "complete") {
            completeForm();
        }
    }

    assistant.addEventListener("click", (event) => {
        const optionButton = event.target.closest("[data-assistant-option]");
        if (optionButton) {
            const question = fieldVoiceTarget || currentQuestion();
            if (question?.element instanceof HTMLSelectElement) {
                applyFieldAnswer(question, optionButton.dataset.assistantOption, {
                    fieldVoice: Boolean(fieldVoiceTarget)
                });
            }
            return;
        }
        if (event.target.closest("[data-assistant-toggle]")) {
            setExpanded(assistant.dataset.expanded !== "true");
            return;
        }
        const button = event.target.closest("[data-assistant-action]");
        if (button) handleAction(button.dataset.assistantAction);
    });
    form.addEventListener("click", (event) => {
        const button = event.target.closest("[data-field-voice-button]");
        if (button) startFieldVoice(button);
    });
    answerInput.addEventListener("keydown", (event) => {
        if (event.key === "Enter") {
            event.preventDefault();
            submitTypedAnswer();
        }
    });
    document.querySelectorAll("[data-page-language]").forEach((button) => {
        button.addEventListener("click", () => {
            setLanguage(button.dataset.pageLanguage);
        });
    });

    if (recognition) {
        recognition.onstart = () => {
            recognitionActive = true;
            recognitionFinalResults = new Set();
            assistant.dataset.listening = "true";
            if (listeningDeadline) {
                updateVoiceCountdown();
            } else {
                setVoiceStatus(fieldVoiceTarget
                    ? copy[language].fieldListening
                    : copy[language].listening);
            }
        };
        recognition.onresult = (event) => {
            if (!listeningDeadline) return;
            let heardSpeech = false;
            const firstChangedResult = Math.max(0, event.resultIndex || 0);
            for (let index = firstChangedResult; index < event.results.length; index += 1) {
                const result = event.results[index];
                const transcript = result[0]?.transcript?.trim() || "";
                if (!transcript) continue;

                heardSpeech = true;
                if (result.isFinal) {
                    if (!recognitionFinalResults.has(index)) {
                        appendTranscript(transcript);
                        recognitionFinalResults.add(index);
                    }
                    pendingInterimTranscript = "";
                } else {
                    pendingInterimTranscript = transcript;
                }
            }
            if (heardSpeech) {
                answerInput.value = getPendingTranscript();
                voiceDetected = true;
                resetSilenceTimer();
            }
        };
        recognition.onerror = (event) => {
            if (event.error === "no-speech" && listeningDeadline > Date.now()) return;
            if (event.error !== "aborted") {
                clearListeningWindow();
                setVoiceStatus(copy[language].recognitionError);
            }
        };
        recognition.onend = () => {
            recognitionActive = false;
            assistant.dataset.listening = "false";
            if (fieldVoiceWaitingForStop && fieldVoiceTarget) {
                fieldVoiceWaitingForStop = false;
                startFieldPrompt();
                return;
            }
            appendTranscript(pendingInterimTranscript);
            pendingInterimTranscript = "";
            if (listeningDeadline > Date.now()) {
                recognitionRestartTimeout = window.setTimeout(() => {
                    recognitionRestartTimeout = 0;
                    if (listeningDeadline > Date.now() && !recognitionActive) {
                        startListening(true);
                    }
                }, 250);
                return;
            }
            if (listeningDeadline) {
                finishListeningAfterSilence();
                return;
            }
            beginListeningWhenReady();
        };
    } else {
        listenButton.disabled = true;
    }

    const storedIndex = Number.parseInt(readStored("index") || "0", 10);
    questionIndex = Number.isFinite(storedIndex) ? Math.max(0, storedIndex) : 0;
    active = readStored("active") === "true";
    if (pendingCode && findUploadedDocument(pendingCode)) {
        const uploaded = findUploadedDocument(pendingCode);
        uploaded.querySelector(".voice-assistant-ocr-review")?.scrollIntoView({ behavior: "smooth", block: "center" });
        setStatus(copy[language].reviewAgain);
    } else {
        setStatus(recognition ? copy[language].ready : copy[language].unsupported);
    }
    setLanguage(language);
})();
